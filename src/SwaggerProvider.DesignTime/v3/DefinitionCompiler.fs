namespace SwaggerProvider.Internal.v3.Compilers

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open UncheckedQuotations
open FSharp.Data.Runtime.NameUtils
open Swagger.Internal
open SwaggerProvider.Internal
open Microsoft.FSharp.Quotations
open Microsoft.OpenApi.Models

type DefinitionPath =
    {
        Namespace: string list
        RequestedTypeName: string
        ProvidedTypeNameCandidate: string
    }

    static member DefinitionPrefix = "#/components/schemas/"

    static member Parse(definition: string) =
        let nsSeparator = '.'

        if (not <| definition.StartsWith(DefinitionPath.DefinitionPrefix)) then
            failwithf $"Definition path ('%s{definition}') does not start with %s{DefinitionPath.DefinitionPrefix}"

        let definitionPath = definition.Substring(DefinitionPath.DefinitionPrefix.Length)

        let rec getCharInTypeName ind =
            if ind = definitionPath.Length then
                ind - 1
            elif
                Char.IsLetterOrDigit definitionPath.[ind]
                || definitionPath.[ind] = nsSeparator
            then
                getCharInTypeName(ind + 1)
            else
                ind

        let lastDot = definitionPath.LastIndexOf(nsSeparator, getCharInTypeName 0)

        if lastDot < 0 then
            {
                Namespace = []
                RequestedTypeName = definitionPath
                ProvidedTypeNameCandidate = nicePascalName definitionPath
            }
        else
            let nsPath =
                definitionPath
                    .Substring(0, lastDot)
                    .Split([| nsSeparator |], StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray

            let tyName = definitionPath.Substring(lastDot + 1)

            {
                Namespace = nsPath
                RequestedTypeName = tyName
                ProvidedTypeNameCandidate = nicePascalName tyName
            }

type NamespaceEntry =
    | Reservation
    | NameAlias
    | ProvidedType of ProvidedTypeDefinition
    | Namespace of NamespaceAbstraction
    | NestedType of ProvidedTypeDefinition * NamespaceAbstraction

and NamespaceAbstraction(name: string) =
    let providedTys = Collections.Generic.Dictionary<string, NamespaceEntry>()

    let updateReservation opName tyName updateFunc =
        match providedTys.TryGetValue tyName with
        | true, Reservation
        | true, NameAlias -> updateFunc()
        | false, _ -> failwithf $"Cannot %s{opName} '%s{tyName}' because name was not reserved"
        | _, value -> failwithf $"Cannot %s{opName} '%s{tyName}' because the slot is used by %A{value}"

    /// Namespace name
    member __.Name = name

    /// Generate unique name and reserve it for the type
    member __.ReserveUniqueName namePref nameSuffix = // TODO: Strange signature - think more
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i = 0 then "" else i.ToString())

            if not <| providedTys.ContainsKey newName then
                newName
            else
                findUniq prefix (i + 1)

        let newName =
            let pref =
                if String.IsNullOrWhiteSpace nameSuffix then namePref
                elif String.IsNullOrWhiteSpace namePref then nameSuffix
                else $"%s{namePref}_%s{nameSuffix}"

            findUniq pref 0

        providedTys.Add(newName, Reservation)
        newName

    /// Release previously reserved name
    member __.ReleaseNameReservation tyName =
        updateReservation "release the name" tyName (fun () -> providedTys.Remove(tyName) |> ignore)

    /// Mark type name as named alias for basic type
    member __.MarkTypeAsNameAlias tyName =
        updateReservation "mark as Alias type" tyName (fun () -> providedTys.[tyName] <- NameAlias)

    /// Associate ProvidedType with reserved type name
    member __.RegisterType(tyName, ty: Type) =
        match ty with
        | :? ProvidedTypeDefinition as ty ->
            match providedTys.TryGetValue tyName with
            | true, Reservation -> providedTys.[tyName] <- ProvidedType ty
            | true, Namespace ns -> providedTys.[tyName] <- NestedType(ty, ns)
            | true, ProvidedType pTy when pTy.Name = tyName -> ()
            | false, _ -> providedTys.[tyName] <- ProvidedType ty
            //failwithf "Cannot register the type '%s' because name was not reserved" tyName
            | _, value -> failwithf $"Cannot register the type '%s{tyName}' because the slot is used by %A{value}"
        | _ -> () // Do nothing, TP should not provide real types

    /// Get or create sub-namespace
    member __.GetOrCreateNamespace name =
        match providedTys.TryGetValue name with
        | true, Namespace ns -> ns
        | true, NestedType(_, ns) -> ns
        | true, ProvidedType ty ->
            let ns = NamespaceAbstraction(name)
            providedTys.[name] <- NestedType(ty, ns)
            ns
        | false, _
        | true, Reservation ->
            let ns = NamespaceAbstraction(name)
            providedTys.[name] <- Namespace ns
            ns
        | true, value -> failwithf $"Name collision, cannot create namespace '%s{name}' because it used by '%A{value}'"

    /// Resolve DefinitionPath according to current namespace
    member this.Resolve(dPath: DefinitionPath) =
        match dPath.Namespace with
        | [] -> this, this.ReserveUniqueName dPath.RequestedTypeName ""
        | name :: tail ->
            let ns = this.GetOrCreateNamespace name
            ns.Resolve { dPath with Namespace = tail }

    /// Create Provided representation of Namespace
    member __.GetProvidedTypes() =
        List.ofSeq providedTys
        |> List.choose(fun kv ->
            match kv.Value with
            | Reservation -> failwithf $"Reservation without type found '%s{kv.Key}'. This is a bug in DefinitionCompiler"
            | NameAlias -> None
            | ProvidedType ty -> Some ty
            | Namespace ns ->
                let types = ns.GetProvidedTypes()

                if types.Length = 0 then
                    None
                else
                    let nsTy = ProvidedTypeDefinition(ns.Name, Some typeof<obj>, isErased = false)

                    nsTy.AddMember
                    <| ProvidedConstructor([], invokeCode = fun _ -> <@@ () @@>) // hack

                    nsTy.AddMembers <| types
                    Some nsTy
            | NestedType(ty, ns) ->
                ty.AddMembers <| ns.GetProvidedTypes()
                Some ty)

/// Object for compiling definitions.
type DefinitionCompiler(schema: OpenApiDocument, provideNullable) as this =
    let pathToSchema =
        if isNull schema.Components then
            Map.empty
        else
            schema.Components.Schemas
            |> Seq.map(fun kv -> DefinitionPath.DefinitionPrefix + kv.Key, kv.Value)
            |> Map.ofSeq

    let pathToType = Collections.Generic.Dictionary<_, Type>()
    let nsRoot = NamespaceAbstraction("Root")
    let nsOps = nsRoot.GetOrCreateNamespace "OperationTypes"

    let generateProperty (scope: UniqueNameGenerator) propName ty =
        let propertyName = scope.MakeUnique <| nicePascalName propName

        let providedField =
            let fieldName = $"_%c{Char.ToLower propertyName.[0]}%s{propertyName.Substring(1)}"

            ProvidedField(fieldName, ty)

        let providedProperty =
            ProvidedProperty(
                propertyName,
                ty,
                getterCode =
                    (function
                    | [ this ] -> Expr.FieldGetUnchecked(this, providedField)
                    | _ -> failwith "invalid property getter params"),
                setterCode =
                    (function
                    | [ this; v ] -> Expr.FieldSetUnchecked(this, providedField, v)
                    | _ -> failwith "invalid property setter params")
            )

        if propName <> propertyName then
            // Override the serialized name by setting a Json-serialization attribute to control the name
            providedProperty.AddCustomAttribute
            <| RuntimeHelpers.getPropertyNameAttribute propName

        providedField, providedProperty

    let registerInNsAndInDef tyPath (ns: NamespaceAbstraction) (name, ty: Type) =
        if not <| pathToType.ContainsKey tyPath then
            pathToType.Add(tyPath, ty)
        //else failwithf "Second time compilation of type definition '%s'. This is a bug in DefinitionCompiler" tyPath

        match ty with
        | :? ProvidedTypeDefinition as prTy -> ns.RegisterType(name, prTy)
        | _ -> ()

    let rec compileByPath(tyPath: string) : Type =
        match pathToType.TryGetValue tyPath with
        | true, ty -> ty
        | false, _ ->
            match pathToSchema.TryFind tyPath with
            | Some def ->
                let ns, tyName = tyPath |> DefinitionPath.Parse |> nsRoot.Resolve
                let ty = compileBySchema ns tyName def true (registerInNsAndInDef tyPath ns) true
                ty :> Type
            | None when tyPath.StartsWith(DefinitionPath.DefinitionPrefix) ->
                failwithf $"Cannot find definition '%s{tyPath}' in schema definitions %A{pathToType.Keys |> Seq.toArray}"
            | None -> failwithf $"Cannot find definition '%s{tyPath}' (references to relative documents are not supported yet)"

    and compileBySchema (ns: NamespaceAbstraction) tyName (schemaObj: OpenApiSchema) isRequired registerNew fromByPathCompiler =
        let compileNewObject() =
            if schemaObj.Properties.Count = 0 then
                if not <| isNull tyName then
                    ns.MarkTypeAsNameAlias tyName

                typeof<obj>
            elif isNull tyName then
                failwithf $"Swagger provider does not support anonymous types: %A{schemaObj}"
            else
                // Register every ProvidedTypeDefinition
                let ty = ProvidedTypeDefinition(tyName, Some typeof<obj>, isErased = false)
                registerNew(tyName, ty :> Type)

                // Generate fields and properties
                let members =
                    let generateProperty = generateProperty(UniqueNameGenerator())

                    List.ofSeq schemaObj.Properties
                    |> List.map(fun p ->
                        let propName, propSchema = p.Key, p.Value

                        if String.IsNullOrEmpty(propName) then
                            failwithf $"Property cannot be created with empty name. TypeName:%A{tyName}; SchemaObj:%A{schemaObj}"

                        let isRequired = schemaObj.Required.Contains(propName)

                        let pTy =
                            compileBySchema ns (ns.ReserveUniqueName tyName (nicePascalName propName)) propSchema isRequired ns.RegisterType false

                        let (pField, pProp) = generateProperty propName pTy

                        if not <| String.IsNullOrWhiteSpace propSchema.Description then
                            pProp.AddXmlDoc propSchema.Description

                        pField, pProp)

                // Add fields and properties to type
                ty.AddMembers
                <| (members
                    |> List.collect(fun (f, p) -> [ f :> MemberInfo; p :> MemberInfo ]))

                // Add default constructor
                ty.AddMember
                <| ProvidedConstructor([], invokeCode = fun _ -> <@@ () @@>)
                // Add full-init constructor
                let ctorParams, fields =
                    let required, optional =
                        List.zip (List.ofSeq schemaObj.Properties) members
                        |> List.partition(fun (x, _) -> schemaObj.Required.Contains(x.Key))

                    (required @ optional)
                    |> List.map(fun (x, (f, p)) ->
                        let paramName = niceCamelName p.Name

                        let prParam =
                            if schemaObj.Required.Contains(x.Key) then
                                ProvidedParameter(paramName, f.FieldType)
                            else
                                let paramDefaultValue = this.GetDefaultValue f.FieldType
                                ProvidedParameter(paramName, f.FieldType, false, paramDefaultValue)

                        prParam, f)
                    |> List.unzip

                ty.AddMember
                <| ProvidedConstructor(
                    ctorParams,
                    invokeCode =
                        fun args ->
                            let (this, args) =
                                match args with
                                | x :: xs -> (x, xs)
                                | _ -> failwith "Wrong constructor arguments"

                            List.zip args fields
                            |> List.map(fun (arg, f) -> Expr.FieldSetUnchecked(this, f, arg))
                            |> List.rev
                            |> List.fold (fun a b -> Expr.Sequential(a, b)) (<@@ () @@>)
                )

                // Override `.ToString()`
                let toStr =
                    ProvidedMethod(
                        "ToString",
                        [],
                        typeof<string>,
                        isStatic = false,
                        invokeCode =
                            fun args ->
                                let this = args.[0]

                                let (pNames, pValues) =
                                    Array.ofList members
                                    |> Array.map(fun (pField, pProp) ->
                                        let pValObj = Expr.FieldGet(this, pField)
                                        pProp.Name, Expr.Coerce(pValObj, typeof<obj>))
                                    |> Array.unzip

                                let pValuesArr = Expr.NewArray(typeof<obj>, List.ofArray pValues)

                                <@@
                                    let values = (%%pValuesArr: array<obj>)

                                    let rec formatValue(v: obj) =
                                        if isNull v then
                                            "null"
                                        else
                                            let vTy = v.GetType()

                                            if vTy = typeof<string> then
                                                String.Format("\"{0}\"", v)
                                            elif vTy.IsArray then
                                                let elements = (v :?> seq<_>) |> Seq.map formatValue
                                                String.Format("[{0}]", String.Join("; ", elements))
                                            else
                                                v.ToString()

                                    let strs =
                                        values
                                        |> Array.mapi(fun i v -> String.Format("{0}={1}", pNames.[i], formatValue v))

                                    String.Format("{{{0}}}", String.Join("; ", strs))
                                @@>
                    )

                toStr.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Virtual)

                let objToStr = (typeof<obj>).GetMethod("ToString", [||])
                ty.DefineMethodOverride(toStr, objToStr)
                ty.AddMember <| toStr

                ty :> Type

        let tyType =
            match schemaObj with
            | null -> failwithf $"Cannot compile object '%s{tyName}' when schema is 'null'"
            | _ when
                schemaObj.Reference <> null
                && not <| schemaObj.Reference.Id.EndsWith(tyName)
                ->
                ns.ReleaseNameReservation tyName
                compileByPath <| schemaObj.Reference.ReferenceV3
            | _ when schemaObj.UnresolvedReference ->
                match pathToType.TryGetValue schemaObj.Reference.ReferenceV3 with
                | true, ty ->
                    ns.ReleaseNameReservation tyName
                    ty
                | _ -> failwithf $"Cannot compile object '%s{tyName}' based on unresolved reference '{schemaObj.Reference.ReferenceV3}'"
            //| _ when schemaObj.Reference <> null && tyName <> schemaObj.Reference.Id ->
            | _ when schemaObj.Type = "object" && schemaObj.AdditionalProperties <> null -> // Dictionary ->
                ns.ReleaseNameReservation tyName
                let elSchema = schemaObj.AdditionalProperties

                let elTy =
                    compileBySchema ns (ns.ReserveUniqueName tyName "Item") elSchema true ns.RegisterType false

                ProvidedTypeBuilder.MakeGenericType(typedefof<Map<string, obj>>, [ typeof<string>; elTy ])
            | _ when schemaObj.Type = null || schemaObj.Type = "object" -> // Object props ->
                compileNewObject()
            | _ ->
                ns.MarkTypeAsNameAlias tyName

                match schemaObj.Type, schemaObj.Format with
                | "integer", "int64" -> typeof<int64>
                | "integer", _ -> typeof<int32>
                | "number", "double" -> typeof<double>
                | "number", _ -> typeof<float32>
                | "boolean", _ -> typeof<bool>
                | "string", "byte" -> typeof<byte>.MakeArrayType (1)
                | "string", "binary" // for `application/octet-stream` request body
                | "file", _ -> // for `multipart/form-data` : https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.2.md#considerations-for-file-uploads
                    typeof<IO.Stream>
                | "string", "date"
                | "string", "date-time" -> typeof<DateTimeOffset>
                | "string", "uuid" -> typeof<Guid>
                | "string", _ -> typeof<string>
                | "array", _ ->
                    ns.ReleaseNameReservation tyName
                    let elSchema = schemaObj.Items

                    let elTy =
                        compileBySchema ns (ns.ReserveUniqueName tyName "Item") elSchema true ns.RegisterType false

                    elTy.MakeArrayType(1)
                | ty, format -> failwithf $"Type %s{tyName}(%s{ty},%s{format}) should be caught by other match statement (%A{schemaObj.Type})"

        if fromByPathCompiler then
            registerNew(tyName, tyType)

        if isRequired then
            tyType
        else if tyType.IsValueType then
            let baseGenTy =
                if provideNullable then
                    typedefof<Nullable<int>>
                else
                    typedefof<Option<obj>>

            ProvidedTypeBuilder.MakeGenericType(baseGenTy, [ tyType ])
        else
            tyType

    // Precompile types defined in the `definitions` part of the schema
    do pathToSchema |> Seq.iter(fun kv -> compileByPath kv.Key |> ignore)

    /// Namespace that represent provided type space
    member __.Namespace = nsRoot

    /// Method that allow OperationCompiler to resolve object reference, compile basic and anonymous types.
    member __.CompileTy opName tyUseSuffix ty required =
        compileBySchema nsOps (nsOps.ReserveUniqueName opName tyUseSuffix) ty required nsOps.RegisterType false

    /// Default value for optional parameters
    member __.GetDefaultValue _ =
        // This method is only used for not required types
        // Reference types, Option<T> and Nullable<T>
        null
