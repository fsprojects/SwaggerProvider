namespace SwaggerProvider.Internal.v2.Compilers

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.v2.Parser.Schema
open Swagger.Internal
open SwaggerProvider.Internal
open Microsoft.FSharp.Quotations

type DefinitionPath =
    { Namespace: string list
      RequestedTypeName: string
      ProvidedTypeNameCandidate: string }

    static member Parse(definition: string) =
        let definitionPrefix, nsSeparator = "#/definitions/", '.'

        if (not <| definition.StartsWith(definitionPrefix)) then
            failwithf $"Definition path does not start with %s{definitionPrefix}"

        let definitionPath = definition.Substring(definitionPrefix.Length)

        let rec getCharInTypeName ind =
            if ind = definitionPath.Length then
                ind - 1
            elif
                Char.IsLetterOrDigit definitionPath[ind]
                || definitionPath[ind] = nsSeparator
            then
                getCharInTypeName(ind + 1)
            else
                ind

        let lastDot = definitionPath.LastIndexOf(nsSeparator, getCharInTypeName 0)

        if lastDot < 0 then
            { Namespace = []
              RequestedTypeName = definitionPath
              ProvidedTypeNameCandidate = nicePascalName definitionPath }
        else
            let nsPath =
                definitionPath
                    .Substring(0, lastDot)
                    .Split([| nsSeparator |], StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray

            let tyName = definitionPath.Substring(lastDot + 1)

            { Namespace = nsPath
              RequestedTypeName = tyName
              ProvidedTypeNameCandidate = nicePascalName tyName }

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
        | true, Reservation -> updateFunc()
        | false, _ -> failwithf $"Cannot %s{opName} '%s{tyName}' because name was not reserved"
        | _, value -> failwithf $"Cannot %s{opName} '%s{tyName}' because the slot is used by %A{value}"

    /// Namespace name
    member _.Name = name

    /// Generate unique name and reserve it for the type
    member _.ReserveUniqueName namePref nameSuffix = // TODO: Strange signature - think more
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i = 0 then "" else i.ToString())

            if not <| providedTys.ContainsKey newName then
                newName
            else
                findUniq prefix (i + 1)

        let newName = findUniq (namePref + nameSuffix) 0
        providedTys.Add(newName, Reservation)
        newName

    /// Release previously reserved name
    member _.ReleaseNameReservation tyName =
        updateReservation "release the name" tyName (fun () -> providedTys.Remove(tyName) |> ignore)

    /// Mark type name as named alias for basic type
    member _.MarkTypeAsNameAlias tyName =
        updateReservation "mark as Alias type" tyName (fun () -> providedTys[tyName] <- NameAlias)

    /// Associate ProvidedType with reserved type name
    member _.RegisterType(tyName, ty) =
        match providedTys.TryGetValue tyName with
        | true, Reservation -> providedTys[tyName] <- ProvidedType ty
        | true, Namespace ns -> providedTys[tyName] <- NestedType(ty, ns)
        | false, _ -> failwithf $"Cannot register the type '%s{tyName}' because name was not reserved"
        | _, value -> failwithf $"Cannot register the type '%s{tyName}' because the slot is used by %A{value}"

    /// Get or create sub-namespace
    member _.GetOrCreateNamespace name =
        match providedTys.TryGetValue name with
        | true, Namespace ns -> ns
        | true, NestedType(_, ns) -> ns
        | true, ProvidedType ty ->
            let ns = NamespaceAbstraction(name)
            providedTys[name] <- NestedType(ty, ns)
            ns
        | false, _
        | true, Reservation ->
            let ns = NamespaceAbstraction(name)
            providedTys[name] <- Namespace ns
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
    member _.GetProvidedTypes() =
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
                    <| ProvidedConstructor([], invokeCode = (fun _ -> <@@ () @@>)) // hack

                    nsTy.AddMembers <| types
                    Some nsTy
            | NestedType(ty, ns) ->
                ty.AddMembers <| ns.GetProvidedTypes()
                Some ty)

/// Object for compiling definitions.
type DefinitionCompiler(schema: SwaggerObject, provideNullable) as this =
    let definitionToSchemaObject = Map.ofSeq schema.Definitions
    let definitionToType = Collections.Generic.Dictionary<_, _>()
    let nsRoot = NamespaceAbstraction("Root")
    let nsOps = nsRoot.GetOrCreateNamespace "OperationTypes"

    let generateProperty (scope: UniqueNameGenerator) propName ty =
        let propertyName = scope.MakeUnique <| nicePascalName propName

        let providedField =
            let fieldName = $"_%c{Char.ToLower propertyName[0]}%s{propertyName.Substring(1)}"

            ProvidedField(fieldName, ty)

        let providedProperty =
            ProvidedProperty(
                propertyName,
                ty,
                getterCode =
                    (function
                    | [ this ] -> Expr.FieldGet(this, providedField)
                    | _ -> failwith "invalid property getter params"),
                setterCode =
                    (function
                    | [ this; v ] -> Expr.FieldSet(this, providedField, v)
                    | _ -> failwith "invalid property setter params")
            )

        if propName <> propertyName then
            providedProperty.AddCustomAttribute
            <| RuntimeHelpers.getPropertyNameAttribute propName

        providedField, providedProperty

    let registerInNsAndInDef tyDefName (ns: NamespaceAbstraction) (name, ty: ProvidedTypeDefinition) =
        if definitionToType.ContainsKey tyDefName then
            failwithf $"Second time compilation of type definition '%s{tyDefName}'. This is a bug in DefinitionCompiler"
        else
            definitionToType.Add(tyDefName, ty)

        ns.RegisterType(name, ty)

    let rec compileDefinition(tyDefName: string) : Type =
        match definitionToType.TryGetValue tyDefName with
        | true, ty -> ty :> Type
        | false, _ ->
            match definitionToSchemaObject.TryFind tyDefName with
            | Some(def) ->
                let ns, tyName = tyDefName |> DefinitionPath.Parse |> nsRoot.Resolve
                let ty = compileSchemaObject ns tyName def true (registerInNsAndInDef tyDefName ns)
                ty :> Type
            | None when tyDefName.StartsWith("#/definitions/") ->
                failwithf $"Cannot find definition '%s{tyDefName}' in schema definitions %A{definitionToType.Keys |> Seq.toArray}"
            | None -> failwithf $"Cannot find definition '%s{tyDefName}' (references to relative documents are not supported yet)"

    and compileSchemaObject (ns: NamespaceAbstraction) tyName (schemaObj: SchemaObject) isRequired registerNew =
        let compileNewObject(properties: DefinitionProperty[]) =
            if properties.Length = 0 then
                if not <| isNull tyName then
                    ns.MarkTypeAsNameAlias tyName

                typeof<obj>
            else if isNull tyName then
                failwithf $"Swagger provider does not support anonymous types: %A{schemaObj}"
            else
                // Register every ProvidedTypeDefinition
                let ty = ProvidedTypeDefinition(tyName, Some typeof<obj>, isErased = false)
                registerNew(tyName, ty)

                // Generate fields and properties
                let members =
                    let generateProperty = generateProperty(UniqueNameGenerator())

                    List.ofArray properties
                    |> List.map(fun p ->
                        if String.IsNullOrEmpty(p.Name) then
                            failwithf $"Property cannot be created with empty name. TypeName:%A{tyName}; SchemaObj:%A{schemaObj}"

                        let pTy =
                            compileSchemaObject ns (ns.ReserveUniqueName tyName (nicePascalName p.Name)) p.Type p.IsRequired ns.RegisterType

                        let pField, pProp = generateProperty p.Name pTy

                        if not <| String.IsNullOrWhiteSpace p.Description then
                            pProp.AddXmlDoc p.Description

                        pField, pProp)

                // Add fields and properties to type
                ty.AddMembers
                <| (members
                    |> List.collect(fun (f, p) -> [ f :> MemberInfo; p :> MemberInfo ]))

                // Add default constructor
                ty.AddMember
                <| ProvidedConstructor([], invokeCode = (fun _ -> <@@ () @@>))
                // Add full-init constructor
                let ctorParams, fields =
                    let required, optional =
                        List.zip (List.ofArray properties) members
                        |> List.partition(fun (x, _) -> x.IsRequired)

                    (required @ optional)
                    |> List.map(fun (x, (f, p)) ->
                        let paramName = niceCamelName p.Name

                        let prParam =
                            if x.IsRequired then
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
                            let this, args =
                                match args with
                                | x :: xs -> (x, xs)
                                | _ -> failwith "Wrong constructor arguments"

                            List.zip args fields
                            |> List.map(fun (arg, f) -> Expr.FieldSet(this, f, arg))
                            |> List.rev
                            |> List.fold (fun a b -> Expr.Sequential(a, b)) <@@ () @@>
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
                                let this = args[0]

                                let pNames, pValues =
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
                                        |> Array.mapi(fun i v -> String.Format("{0}={1}", pNames[i], formatValue v))

                                    String.Format("{{{0}}}", String.Join("; ", strs))
                                @@>
                    )

                toStr.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Virtual)

                let objToStr = typeof<obj>.GetMethod("ToString", [||])
                ty.DefineMethodOverride(toStr, objToStr)
                ty.AddMember <| toStr

                ty :> Type

        let tyType =
            match schemaObj with
            | Reference path ->
                ns.ReleaseNameReservation tyName
                compileDefinition path
            | Object props -> compileNewObject props
            | _ ->
                ns.MarkTypeAsNameAlias tyName

                match schemaObj with
                | Boolean -> typeof<bool>
                | Byte -> typeof<byte>
                | Int32 -> typeof<int32>
                | Int64 -> typeof<int64>
                | Float -> typeof<float32>
                | Double -> typeof<double>
                | String -> typeof<string>
                | Date
                | DateTime -> typeof<DateTime>
                | File -> typeof<byte>.MakeArrayType 1
                | Enum(_, "string") -> typeof<string>
                | Enum(_, "boolean") -> typeof<bool>
                | Enum _ -> typeof<int32>
                | Array eTy ->
                    (compileSchemaObject ns (ns.ReserveUniqueName tyName "Item") eTy true ns.RegisterType)
                        .MakeArrayType(1)
                | Dictionary eTy ->
                    ProvidedTypeBuilder.MakeGenericType(
                        typedefof<Map<string, obj>>,
                        [ typeof<string>
                          compileSchemaObject ns (ns.ReserveUniqueName tyName "Item") eTy false ns.RegisterType ]
                    )
                | Reference _
                | Object _ -> failwith "This case should be caught by other match statement"

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
    do
        schema.Definitions
        |> Seq.iter(fun (name, _) -> compileDefinition name |> ignore)

    /// Namespace that represent provided type space
    member _.Namespace = nsRoot

    /// Method that allow OperationCompiler to resolve object reference, compile basic and anonymous types.
    member _.CompileTy opName tyUseSuffix ty required =
        compileSchemaObject nsOps (nsOps.ReserveUniqueName opName tyUseSuffix) ty required nsOps.RegisterType

    /// Default value for optional parameters
    member _.GetDefaultValue _ =
        // This method is only used for not required types
        // Reference types, Option<T> and Nullable<T>
        null
