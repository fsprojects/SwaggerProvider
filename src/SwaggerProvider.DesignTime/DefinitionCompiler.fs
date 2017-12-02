namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open UncheckedQuotations
open FSharp.Data.Runtime.NameUtils
open Swagger.Parser.Schema
open SwaggerProvider.Internal
open Microsoft.FSharp.Quotations
open System
open System.Reflection

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerObject, provideNullable) as this =
    let definitions = Map.ofSeq schema.Definitions
    let definitionTys = Collections.Generic.Dictionary<_,_>()

    let providedTys = Collections.Generic.Dictionary<_,_>()
    let uniqueName namePref suffix =
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i=0 then "" else i.ToString())
            if not <| providedTys.ContainsKey newName
            then newName else findUniq prefix (i+1)
        let newName = findUniq (namePref+suffix) 0
        providedTys.Add(newName, None)
        newName
    let tysNameScope = UniqueNameGenerator()

    let generateProperty propName ty (scope:UniqueNameGenerator) =
        let propertyName = scope.MakeUnique <| nicePascalName propName
        let providedField = ProvidedField("_" + propertyName.ToLower(), ty)
        let providedProperty =
            ProvidedProperty(propertyName, ty,
                getterCode = (fun [this] -> Expr.FieldGetUnchecked (this, providedField)),
                setterCode = (fun [this;v] -> Expr.FieldSetUnchecked(this, providedField, v)))
        if propName <> propertyName then
            providedProperty.AddCustomAttribute
                <| RuntimeHelpers.getPropertyNameAttribute propName
        providedField, providedProperty

    let rec compileDefinition (tyDefName:string) =
        match definitionTys.TryGetValue tyDefName with
        | true, ty -> ty
        | false, _ ->
            match definitions.TryFind tyDefName with
            | Some(def) ->
                let tyName = tyDefName.Substring("#/definitions/".Length).Replace(".","")
                let ty = compileSchemaObject tyName def true
                if not <| definitions.ContainsKey tyDefName
                    then definitionTys.Add(tyDefName, ty)
                ty
            | None ->
                let tys = definitionTys.Keys |> Seq.toArray
                failwithf "Unknown definition '%s' in definitionTys %A" tyDefName tys
    and compileSchemaObject tyName (schemaObj:SchemaObject) isRequired =
        let compileNewObject (properties:DefinitionProperty[]) =
            if properties.Length = 0 then typeof<obj>
            else
              if isNull tyName then
                failwithf "Swagger provider does not support anonymous types: %A" schemaObj
              else
                // Register every ProvidedTypeDefinition
                match providedTys.TryGetValue tyName with
                | true, Some(ty) -> ty :> Type
                | isExist, _ ->
                    let tyNiceName = tysNameScope.MakeUnique <| nicePascalName(tyName)
                    let ty = ProvidedTypeDefinition(tyNiceName, Some typeof<obj>, isErased = false)
                    if isExist
                    then providedTys.[tyName] <- Some(ty)
                    else providedTys.Add(tyName, Some(ty))

                    // Generate fields and properties
                    let members =
                        let propsNameScope = UniqueNameGenerator()
                        List.ofArray properties
                        |> List.map (fun p ->
                            if String.IsNullOrEmpty(p.Name)
                                then failwithf "Property cannot be created with empty name. Obj name:%A; ObjSchema:%A" tyName schemaObj

                            let pTy = compileSchemaObject (uniqueName tyName (nicePascalName p.Name)) p.Type p.IsRequired
                            let (pField, pProp) = generateProperty p.Name pTy propsNameScope
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
                                                    seq {
                                                        for x in (v :?> Collections.IEnumerable) do
                                                            yield formatValue x
                                                    } |> Seq.toArray
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
            | Boolean   -> typeof<bool>
            | Byte      -> typeof<byte>
            | Int32     -> typeof<int32>
            | Int64     -> typeof<int64>
            | Float     -> typeof<float32>
            | Double    -> typeof<double>
            | String    -> typeof<string>
            | Date | DateTime -> typeof<DateTime>
            | File            -> typeof<byte>.MakeArrayType(1)
            | Enum _          -> typeof<string> //TODO: find better type
            | Array eTy       -> (compileSchemaObject (uniqueName tyName "Item") eTy true).MakeArrayType()
            | Dictionary eTy  -> ProvidedTypeBuilder.MakeGenericType(typedefof<Map<string, obj>>, 
                                        [typeof<string>; compileSchemaObject (uniqueName tyName "Item") eTy false])
            | Object props    -> compileNewObject props
            | Reference path  -> compileDefinition path
        if isRequired then tyType
        else 
            if provideNullable then 
                if tyType.IsValueType
                then ProvidedTypeBuilder.MakeGenericType(typedefof<Nullable<int>>, [tyType])
                else tyType
            else 
                ProvidedTypeBuilder.MakeGenericType(typedefof<Option<obj>>, [tyType])

    // Compiles the `definitions` part of the schema
    do  schema.Definitions
        |> Seq.iter (fun (name,_) ->
            compileDefinition name |> ignore)

    /// Compiles the definition.
    member __.GetProvidedTypes() =
        List.ofSeq providedTys.Values
        |> List.choose (id)

    /// Compiles the definition.
    member __.CompileTy opName tyUseSuffix ty required =
        compileSchemaObject (uniqueName opName tyUseSuffix) ty required

    member __.GetDefaultValue _ =
        // This method is only used for not requiried types
        // Reference types, Option<T> and Nullable<T>
        null
