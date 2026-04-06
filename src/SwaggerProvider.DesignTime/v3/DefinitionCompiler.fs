namespace SwaggerProvider.Internal.v3.Compilers

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open UncheckedQuotations
open FSharp.Data.Runtime.NameUtils
open Swagger.Internal
open SwaggerProvider.Internal
open Microsoft.FSharp.Quotations
open Microsoft.OpenApi

type DefinitionPath =
    { Namespace: string list
      RequestedTypeName: string
      ProvidedTypeNameCandidate: string }

    static member DefinitionPrefix = "#/components/schemas/"

    static member Parse(definition: string) =
        let nsSeparator = '.'

        if not <| definition.StartsWith DefinitionPath.DefinitionPrefix then
            failwithf $"Definition path ('%s{definition}') does not start with %s{DefinitionPath.DefinitionPrefix}"

        let definitionPath = definition.Substring DefinitionPath.DefinitionPrefix.Length

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
                definitionPath.Substring(0, lastDot).Split([| nsSeparator |], StringSplitOptions.RemoveEmptyEntries)
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
        | true, Reservation
        | true, NameAlias -> updateFunc()
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

        let newName =
            let pref =
                if String.IsNullOrWhiteSpace nameSuffix then namePref
                elif String.IsNullOrWhiteSpace namePref then nameSuffix
                else $"%s{namePref}_%s{nameSuffix}"

            findUniq pref 0

        providedTys.Add(newName, Reservation)
        newName

    /// Release previously reserved name
    member _.ReleaseNameReservation tyName =
        updateReservation "release the name" tyName (fun () -> providedTys.Remove tyName |> ignore)

    /// Mark type name as named alias for basic type
    member _.MarkTypeAsNameAlias tyName =
        updateReservation "mark as Alias type" tyName (fun () -> providedTys[tyName] <- NameAlias)

    /// Associate ProvidedType with reserved type name
    member _.RegisterType(tyName, ty: Type) =
        match ty with
        | :? ProvidedTypeDefinition as ty ->
            match providedTys.TryGetValue tyName with
            | true, Reservation -> providedTys[tyName] <- ProvidedType ty
            | true, Namespace ns -> providedTys[tyName] <- NestedType(ty, ns)
            | true, ProvidedType pTy when pTy.Name = tyName -> ()
            | false, _ -> providedTys[tyName] <- ProvidedType ty
            //failwithf "Cannot register the type '%s' because name was not reserved" tyName
            | _, value -> failwithf $"Cannot register the type '%s{tyName}' because the slot is used by %A{value}"
        | _ -> () // Do nothing, TP should not provide real types

    /// Get or create sub-namespace
    member _.GetOrCreateNamespace name =
        match providedTys.TryGetValue name with
        | true, Namespace ns -> ns
        | true, NestedType(_, ns) -> ns
        | true, ProvidedType ty ->
            let ns = NamespaceAbstraction name
            providedTys[name] <- NestedType(ty, ns)
            ns
        | false, _
        | true, Reservation ->
            let ns = NamespaceAbstraction name
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
type DefinitionCompiler(schema: OpenApiDocument, provideNullable, useDateOnly: bool) as this =
    let pathToSchema =
        let dict = Collections.Generic.Dictionary<string, IOpenApiSchema>()

        if not(isNull schema.Components) then
            for kv in schema.Components.Schemas do
                dict.Add(DefinitionPath.DefinitionPrefix + kv.Key, kv.Value)

        dict

    let pathToType = Collections.Generic.Dictionary<_, Type>()
    let nsRoot = NamespaceAbstraction "Root"
    let nsOps = nsRoot.GetOrCreateNamespace "OperationTypes"

    let generateProperty (scope: UniqueNameGenerator) propName ty =
        let propertyName = scope.MakeUnique <| nicePascalName propName

        let providedField =
            let fieldName = $"_%c{Char.ToLower propertyName[0]}%s{propertyName.Substring 1}"

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
            match pathToSchema.TryGetValue tyPath with
            | true, def ->
                let ns, tyName = tyPath |> DefinitionPath.Parse |> nsRoot.Resolve
                let ty = compileBySchema ns tyName def true (registerInNsAndInDef tyPath ns) true
                ty :> Type
            | false, _ when tyPath.StartsWith DefinitionPath.DefinitionPrefix ->
                failwithf $"Cannot find definition '%s{tyPath}' in schema definitions %A{pathToType.Keys |> Seq.toArray}"
            | _ -> failwithf $"Cannot find definition '%s{tyPath}' (references to relative documents are not supported yet)"

    and compileBySchema (ns: NamespaceAbstraction) tyName (schemaObj: IOpenApiSchema) isRequired registerNew fromByPathCompiler =
        let compileNewObject() =
            let inline toSeq x =
                if isNull x then Seq.empty else x :> seq<_>

            let properties = schemaObj.Properties |> toSeq
            let allOf = schemaObj.AllOf |> toSeq

            if Seq.isEmpty properties && Seq.isEmpty allOf then
                if not <| isNull tyName then
                    ns.MarkTypeAsNameAlias tyName

                typeof<obj>
            elif isNull tyName then
                failwithf $"Swagger provider does not support anonymous types: %A{schemaObj}"
            else
                // Register every ProvidedTypeDefinition
                let ty = ProvidedTypeDefinition(tyName, Some typeof<obj>, isErased = false)
                registerNew(tyName, ty :> Type)

                // Combine composite schemas
                let schemaObjProperties =
                    let getProps(s: IOpenApiSchema) =
                        s.Properties |> toSeq

                    match Seq.isEmpty allOf with
                    | false -> allOf |> Seq.append [ schemaObj ] |> Seq.collect getProps
                    | true -> getProps schemaObj


                let schemaObjRequired =
                    let getReq(s: IOpenApiSchema) =
                        s.Required |> toSeq

                    match Seq.isEmpty allOf with
                    | false -> allOf |> Seq.append [ schemaObj ] |> Seq.collect getReq
                    | true -> getReq schemaObj
                    |> Set.ofSeq

                // Helper to check if a schema has the Null type flag (OpenAPI 3.0 nullable)
                let isSchemaNullable(schema: IOpenApiSchema) =
                    not(isNull schema)
                    && schema.Type.HasValue
                    && schema.Type.Value.HasFlag(JsonSchemaType.Null)

                // Generate fields and properties
                let members =
                    let generateProperty = generateProperty(UniqueNameGenerator())

                    List.ofSeq schemaObjProperties
                    |> List.map(fun p ->
                        let propName, propSchema = p.Key, p.Value

                        if String.IsNullOrEmpty propName then
                            failwithf $"Property cannot be created with empty name. TypeName:%A{tyName}; SchemaObj:%A{schemaObj}"

                        // Check if the property is nullable (OpenAPI 3.0 nullable becomes Null type flag in 3.1)
                        let isNullable = isSchemaNullable propSchema

                        // A property is "required" for type generation if it's in the required list AND not nullable.
                        // Nullable properties must be wrapped as Option<T>/Nullable<T> to represent null values,
                        // even if they're in the required list (required + nullable means must be present but can be null).
                        let isRequired = schemaObjRequired.Contains propName && not isNullable

                        let pTy =
                            compileBySchema ns (ns.ReserveUniqueName tyName (nicePascalName propName)) propSchema isRequired ns.RegisterType false

                        let pField, pProp = generateProperty propName pTy

                        let formatEnumValue(v: System.Text.Json.Nodes.JsonNode) =
                            if isNull v then
                                "null"
                            else
                                // Format known JsonNode scalar types directly so documentation does not depend
                                // on JSON serialization/escaping or specific ToString() implementations.
                                match v with
                                | :? System.Text.Json.Nodes.JsonValue as jv ->
                                    match jv.GetValueKind() with
                                    | System.Text.Json.JsonValueKind.String -> jv.GetValue<string>()
                                    | System.Text.Json.JsonValueKind.Null -> "null"
                                    | _ -> jv.ToString()
                                | _ -> v.ToString()

                        let enumValuesDoc =
                            if not(isNull propSchema.Enum) && propSchema.Enum.Count > 0 then
                                let values = propSchema.Enum |> Seq.map formatEnumValue |> String.concat ", "

                                Some $"Allowed values: {values}"
                            else
                                None

                        let propDoc =
                            match
                                propSchema.Description
                                |> Option.ofObj
                                |> Option.filter(String.IsNullOrWhiteSpace >> not),
                                enumValuesDoc
                            with
                            | None, None -> null
                            | Some d, None -> d
                            | None, Some ev -> ev
                            | Some d, Some ev -> $"{d}\n{ev}"

                        if not(isNull propDoc) then
                            pProp.AddXmlDoc propDoc

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
                        List.zip (List.ofSeq schemaObjProperties) members
                        |> List.partition(fun (x, _) ->
                            let isNullable = isSchemaNullable x.Value
                            schemaObjRequired.Contains x.Key && not isNullable)

                    required @ optional
                    |> List.map(fun (x, (f, p)) ->
                        let paramName = niceCamelName p.Name
                        let isNullable = isSchemaNullable x.Value

                        let prParam =
                            if schemaObjRequired.Contains x.Key && not isNullable then
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
                                | x :: xs -> x, xs
                                | _ -> failwith "Wrong constructor arguments"

                            List.zip args fields
                            |> List.map(fun (arg, f) -> Expr.FieldSetUnchecked(this, f, arg))
                            |> List.rev
                            |> List.fold (fun a b -> Expr.Sequential(a, b)) <@@ () @@>
                )

                // Override `.ToString()`
                // Delegates to the shared RuntimeHelpers.formatObject helper so that
                // each generated type's method body is a single static call (O(1) IL).
                let toStr =
                    ProvidedMethod(
                        "ToString",
                        [],
                        typeof<string>,
                        isStatic = false,
                        invokeCode =
                            fun args ->
                                let this = args[0]
                                let thisObj = Expr.Coerce(this, typeof<obj>)
                                <@@ RuntimeHelpers.formatObject(%%thisObj: obj) @@>
                    )

                toStr.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Virtual)

                let objToStr = typeof<obj>.GetMethod("ToString", [||])
                ty.DefineMethodOverride(toStr, objToStr)
                ty.AddMember <| toStr

                ty :> Type

        let resolvedType =
            // If schemaObj.Type is missing, but allOf is present andallOf subschema has one element, use that
            if
                not schemaObj.Type.HasValue
                && not(isNull schemaObj.AllOf)
                && schemaObj.AllOf.Count = 1
            then
                let firstAllOf = schemaObj.AllOf.[0]

                if not(isNull firstAllOf) && firstAllOf.Type.HasValue then
                    Some firstAllOf.Type.Value
                else
                    None
            else if schemaObj.Type.HasValue then
                Some schemaObj.Type.Value
            else
                None

        // Helper to get full definition path from reference ID
        let getFullPath(refId: string) =
            if refId.StartsWith DefinitionPath.DefinitionPrefix then
                refId
            else
                DefinitionPath.DefinitionPrefix + refId

        let tyType =
            match schemaObj with
            | null -> failwithf $"Cannot compile object '%s{tyName}' when schema is 'null'"
            | :? OpenApiSchemaReference as schemaRef when
                not(isNull schemaRef.Reference)
                && not <| schemaRef.Reference.Id.EndsWith tyName
                ->
                ns.ReleaseNameReservation tyName
                compileByPath <| getFullPath schemaRef.Reference.Id
            | :? OpenApiSchemaReference as schemaRef when not(isNull schemaRef.Reference) ->
                let fullPath = getFullPath schemaRef.Reference.Id

                match pathToType.TryGetValue fullPath with
                | true, ty ->
                    ns.ReleaseNameReservation tyName
                    ty
                | _ -> failwithf $"Cannot compile object '%s{tyName}' based on unresolved reference '{schemaRef.Reference.Id}'"
            // TODO: fail on external references
            //| _ when schemaObj.Reference <> null && tyName <> schemaObj.Reference.Id ->
            | _ when
                resolvedType = Some JsonSchemaType.Object
                && not(isNull schemaObj.AdditionalProperties)
                -> // Dictionary ->
                ns.ReleaseNameReservation tyName
                let elSchema = schemaObj.AdditionalProperties

                let elTy =
                    compileBySchema ns (ns.ReserveUniqueName tyName "Item") elSchema true ns.RegisterType false

                ProvidedTypeBuilder.MakeGenericType(typedefof<Map<string, obj>>, [ typeof<string>; elTy ])
            // Handle allOf with single reference (e.g., nullable reference to another type)
            | _ when
                not(isNull schemaObj.AllOf)
                && schemaObj.AllOf.Count = 1
                && (schemaObj.Properties |> isNull || schemaObj.Properties.Count = 0)
                ->
                match schemaObj.AllOf.[0] with
                | :? OpenApiSchemaReference as schemaRef when not(isNull schemaRef.Reference) ->
                    ns.ReleaseNameReservation tyName
                    compileByPath <| getFullPath schemaRef.Reference.Id
                | _ -> compileNewObject()
            | _ when
                resolvedType.IsNone
                || resolvedType = Some JsonSchemaType.Object
                || resolvedType = Some JsonSchemaType.Null
                || resolvedType = Some(JsonSchemaType.Null ||| JsonSchemaType.Object)
                ->
                compileNewObject()
            | _ ->
                ns.MarkTypeAsNameAlias tyName

                match resolvedType with
                | None -> failwithf $"Schema type is not specified for '%s{tyName}'"
                | Some t ->
                    let (|HasFlag|_|) (flag: JsonSchemaType) (value: JsonSchemaType) =
                        if value.HasFlag flag then Some() else None

                    match t, schemaObj.Format with
                    | HasFlag JsonSchemaType.Boolean, _ -> typeof<bool>
                    | HasFlag JsonSchemaType.Integer, "int64" -> typeof<int64>
                    | HasFlag JsonSchemaType.Integer, _ -> typeof<int32>
                    | HasFlag JsonSchemaType.Number, "double" -> typeof<double>
                    | HasFlag JsonSchemaType.Number, _ -> typeof<float32>
                    | HasFlag JsonSchemaType.String, "byte" -> typeof<byte>.MakeArrayType 1
                    | HasFlag JsonSchemaType.String, "binary" ->
                        // for `application/octet-stream` request body
                        // for `multipart/form-data` : https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.2.md#considerations-for-file-uploads
                        typeof<IO.Stream>
                    | HasFlag JsonSchemaType.String, "date" ->
                        // Use DateOnly only when the target runtime supports it (.NET 6+).
                        // We check useDateOnly (derived from cfg.SystemRuntimeAssemblyVersion) rather than
                        // probing the design-time host process, which may differ from the consumer's runtime.
                        if useDateOnly then
                            System.Type.GetType("System.DateOnly")
                            |> Option.ofObj
                            |> Option.defaultValue typeof<DateTimeOffset>
                        else
                            typeof<DateTimeOffset>
                    | HasFlag JsonSchemaType.String, "date-time" -> typeof<DateTimeOffset>
                    | HasFlag JsonSchemaType.String, "uuid" -> typeof<Guid>
                    | HasFlag JsonSchemaType.String, _ -> typeof<string>
                    | HasFlag JsonSchemaType.Array, _ ->
                        ns.ReleaseNameReservation tyName
                        let elSchema = schemaObj.Items

                        let elTy =
                            compileBySchema ns (ns.ReserveUniqueName tyName "Item") elSchema true ns.RegisterType false

                        elTy.MakeArrayType 1
                    | ty, format -> failwithf $"Type %s{tyName}(%A{ty},%s{format}) should be caught by other match statement (%A{resolvedType})"

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
    do
        pathToSchema.Keys
        |> Seq.sort
        |> Seq.iter(fun key -> compileByPath key |> ignore)

    /// Namespace that represent provided type space
    member _.Namespace = nsRoot

    /// Method that allow OperationCompiler to resolve object reference, compile basic and anonymous types.
    member _.CompileTy opName tyUseSuffix ty required =
        compileBySchema nsOps (nsOps.ReserveUniqueName opName tyUseSuffix) ty required nsOps.RegisterType false

    /// Default value for optional parameters
    member _.GetDefaultValue _ =
        // This method is only used for not required types
        // Reference types, Option<T> and Nullable<T>
        null
