namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open UncheckedQuotations
open FSharp.Data.Runtime.NameUtils
open Swagger.Parser.Schema
open SwaggerProvider.Internal
open Microsoft.FSharp.Quotations
open System
open System.Reflection

type NamespaceEntry =
    | Reservation
    | NameAlias
    | ProvidedType of ProvidedTypeDefinition
    | Namespace of NamespaceAbstraction
and NamespaceAbstraction (name:string) =
    let providedTys = Collections.Generic.Dictionary<string,NamespaceEntry>()
    let updateReservation opName tyName updateFunc =
        match providedTys.TryGetValue tyName with
        | true, Reservation -> updateFunc()
        | false, _ -> failwithf "Cannot %s '%s' because name was not reserved" opName tyName
        | _, value -> failwithf "Cannot %s '%s' because the slot is used by %A" opName tyName value 

    /// Namespace name
    member __.Name = name
    /// Generate unique name and reserve it for the type
    member __.ReserveUniqueName namePref nameSuffix = // TODO: Strange signature - think more
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i=0 then "" else i.ToString())
            if not <| providedTys.ContainsKey newName
            then newName 
            else findUniq prefix (i+1)
        let newName = findUniq (namePref+nameSuffix) 0
        providedTys.Add(newName, Reservation)
        newName
    /// Release previously reserved name
    member __.ReleaseNameReservation tyName =
        updateReservation "release the name" tyName (fun() -> providedTys.Remove(tyName) |> ignore)
    /// Associate ProvidedType with reserved type name
    member __.RegisterType (tyName,value) =
        updateReservation "register the type" tyName (fun() -> providedTys.[tyName] <- ProvidedType value)
    /// Mark type name as named alias for basic type
    member __.MarkTypeAsNameAlias tyName =
        updateReservation "mark as Alias type" tyName (fun() -> providedTys.[tyName] <- NameAlias)
    /// Create Provided representation of Namespace
    member __.GetProvidedTypes() =
        List.ofSeq providedTys
        |> List.choose (fun kv ->
            match kv.Value with
            | Reservation ->
                failwithf "Reservation without type found '%s'. This is a bug in DefinitionCompiler" kv.Key
            | NameAlias -> None
            | ProvidedType ty -> Some ty
            | Namespace ns ->
                let nsTy = ProvidedTypeDefinition(ns.Name, Some typeof<obj>, isErased = false, hideObjectMethods = true)
                nsTy.AddMembers <| ns.GetProvidedTypes()
                Some nsTy)

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerObject, provideNullable) as this =
    let definitionToSchemaObject = Map.ofSeq schema.Definitions
    let definitionToType = Collections.Generic.Dictionary<_,_>()
    let ns = NamespaceAbstraction("Root")

    let generateProperty (scope:UniqueNameGenerator) propName ty =
        let propertyName = scope.MakeUnique <| nicePascalName propName
        let providedField = 
            let fieldName = sprintf "_%c%s" (Char.ToLower propertyName.[0]) (propertyName.Substring(1))
            ProvidedField(fieldName, ty)
        let providedProperty =
            ProvidedProperty(propertyName, ty,
                getterCode = (fun [this] -> Expr.FieldGetUnchecked (this, providedField)),
                setterCode = (fun [this;v] -> Expr.FieldSetUnchecked(this, providedField, v)))
        if propName <> propertyName then
            providedProperty.AddCustomAttribute
                <| RuntimeHelpers.getPropertyNameAttribute propName
        providedField, providedProperty
    
    let registerInNs = ns.RegisterType
    let registerInNsAndInDef tyDefName (name, ty: ProvidedTypeDefinition) =
        if definitionToType.ContainsKey tyDefName
        then failwithf "Second time compilation of type defition '%s'. This is a bug in DefinitionCompiler" tyDefName
        else definitionToType.Add(tyDefName, ty)
        registerInNs (name, ty)

    let rec compileDefinition (tyDefName:string) : Type=
        match definitionToType.TryGetValue tyDefName with
        | true, ty -> ty :> Type
        | false, _ ->
            match definitionToSchemaObject.TryFind tyDefName with
            | Some(def) ->
                let tyName = tyDefName.Substring("#/definitions/".Length)
                             |> nicePascalName // TODO: Support namespaces here
                let tyName = ns.ReserveUniqueName tyName ""
                let ty = compileSchemaObject tyName def true (registerInNsAndInDef tyDefName)
                ty :> Type
            | None ->
                failwithf "Cannot find definition '%s' in schema definitions %A" 
                    tyDefName (definitionToType.Keys |> Seq.toArray)
    and compileSchemaObject tyName (schemaObj:SchemaObject) isRequired registerNew =
        let compileNewObject (properties:DefinitionProperty[]) =
            if properties.Length = 0 
            then
                if not <| isNull tyName then
                    ns.MarkTypeAsNameAlias tyName 
                typeof<obj>
            else
              if isNull tyName then
                failwithf "Swagger provider does not support anonymous types: %A" schemaObj
              else
                // Register every ProvidedTypeDefinition
                let ty = ProvidedTypeDefinition(tyName, Some typeof<obj>, isErased = false)
                registerNew(tyName,ty)

                // Generate fields and properties
                let members =
                    let generateProperty = generateProperty (UniqueNameGenerator())
                    List.ofArray properties
                    |> List.map (fun p ->
                        if String.IsNullOrEmpty(p.Name)
                            then failwithf "Property cannot be created with empty name. TypeName:%A; SchemaObj:%A" tyName schemaObj

                        let pTy = compileSchemaObject (ns.ReserveUniqueName tyName (nicePascalName p.Name)) p.Type p.IsRequired registerInNs
                        let (pField, pProp) = generateProperty p.Name pTy
                        if not <| String.IsNullOrWhiteSpace p.Description
                            then pProp.AddXmlDoc p.Description
                        pField, pProp
                    )

                // Add fields and properties to type
                ty.AddMembers <|
                    (members |> List.collect (fun (f,p) -> [f :> MemberInfo; p:> MemberInfo]))

                // Add default constructor
                ty.AddMember <| ProvidedConstructor([], invokeCode = fun _ -> <@@ () @@>)
                // Add full-init constructor
                let ctorParams, fields =
                    let required, optional = 
                        List.zip (List.ofArray properties) members
                        |> List.partition (fun (x,_) -> x.IsRequired)
                    (required @ optional)
                    |> List.map(fun (x,(f,p)) -> 
                        let paramName = niceCamelName p.Name
                        let prParam = 
                            if x.IsRequired
                            then ProvidedParameter(paramName, f.FieldType) 
                            else
                                let paramDefaultValue = this.GetDefaultValue f.FieldType
                                ProvidedParameter(paramName, f.FieldType, false, paramDefaultValue)
                        prParam, f)
                    |> List.unzip
                ty.AddMember <| ProvidedConstructor(ctorParams, invokeCode = fun args ->
                    let (this,args) = 
                        match args with
                        | x::xs -> (x,xs)
                        | _ -> failwith "Wrong constructor arguments"
                    List.zip args fields
                    |> List.map (fun (arg, f) ->
                         Expr.FieldSetUnchecked(this, f, arg))
                    |> List.rev
                    |> List.fold (fun a b -> 
                        Expr.Sequential(a, b)) (<@@ () @@>)
                    )

                // Override `.ToString()`
                let toStr = 
                    ProvidedMethod("ToString", [], typeof<string>, isStatic = false, 
                        invokeCode = fun args ->
                            let this = args.[0]
                            let (pNames, pValues) =
                                Array.ofList members
                                |> Array.map (fun (pField, pProp) ->
                                    let pValObj = Expr.FieldGet(this, pField)
                                    pProp.Name, Expr.Coerce(pValObj, typeof<obj>)
                                   )
                                |> Array.unzip
                            let pValuesArr = Expr.NewArray(typeof<obj>, List.ofArray pValues)
                            <@@
                                let values = (%%pValuesArr : array<obj>)
                                let rec formatValue (v:obj) =
                                    if isNull v then "null"
                                    else
                                        let vTy = v.GetType()
                                        if vTy = typeof<string>
                                        then String.Format("\"{0}\"",v)
                                        elif vTy.IsArray
                                        then
                                            let elements =
                                                [| for x in (v :?> Collections.IEnumerable) do
                                                     yield formatValue x 
                                                |]
                                            String.Format("[{0}]", String.Join("; ", elements))
                                        else v.ToString()

                                let strs = values |> Array.mapi (fun i v ->
                                    String.Format("{0}={1}",pNames.[i], formatValue v))
                                String.Format("{{{0}}}", String.Join("; ",strs))
                            @@>)
                toStr.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Virtual)

                let objToStr = (typeof<obj>).GetMethod("ToString",[||])
                ty.DefineMethodOverride(toStr, objToStr)
                ty.AddMember <| toStr

                ty :> Type
        let tyType =
            match schemaObj with
            | Reference path  ->
                ns.ReleaseNameReservation tyName
                compileDefinition path
            | Object props -> 
                compileNewObject props
            | _ ->
                ns.MarkTypeAsNameAlias tyName
                match schemaObj with
                | Boolean   -> typeof<bool>
                | Byte      -> typeof<byte>
                | Int32     -> typeof<int32>
                | Int64     -> typeof<int64>
                | Float     -> typeof<float32>
                | Double    -> typeof<double>
                | String    -> typeof<string>
                | Date | DateTime -> typeof<DateTime>
                | File            -> typeof<byte>.MakeArrayType(1)
                | Enum _          -> typeof<string> //NOTE: find better type
                | Array eTy       -> 
                    (compileSchemaObject (ns.ReserveUniqueName tyName "Item") eTy true registerInNs).MakeArrayType(1)
                | Dictionary eTy  -> 
                    ProvidedTypeBuilder.MakeGenericType(typedefof<Map<string, obj>>, 
                        [typeof<string>; compileSchemaObject (ns.ReserveUniqueName tyName "Item") eTy false registerInNs])
                | Reference _ 
                | Object _  -> failwith "This case should be catched by other match statement"
        if isRequired then tyType
        else 
            if provideNullable then 
                if tyType.IsValueType
                then ProvidedTypeBuilder.MakeGenericType(typedefof<Nullable<int>>, [tyType])
                else tyType
            else 
                ProvidedTypeBuilder.MakeGenericType(typedefof<Option<obj>>, [tyType])

    // Precompile types defined in the `definitions` part of the schema
    do  schema.Definitions
        |> Seq.iter (fun (name,_) ->
            compileDefinition name |> ignore)

    /// Namespace that represent provided type space
    member __.Namespace = ns

    /// Method that allow OperationComplier to resolve object reference, compile basic and anonymous types.
    member __.CompileTy opName tyUseSuffix ty required =
        compileSchemaObject (ns.ReserveUniqueName opName tyUseSuffix) ty required registerInNs

    /// Default value for optional parameters
    member __.GetDefaultValue _ =
        // This method is only used for not requiried types
        // Reference types, Option<T> and Nullable<T>
        null
