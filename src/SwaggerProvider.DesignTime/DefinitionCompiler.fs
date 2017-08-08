namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal
open SwaggerProvider.Internal.Schema
open Microsoft.FSharp.Quotations
open System
open System.Reflection

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerObject) =
    let definitions = Map.ofSeq schema.Definitions
    let definitionTys = System.Collections.Generic.Dictionary<_,_>()

    let providedTys = System.Collections.Generic.Dictionary<_,_>()
    let uniqueName namePref suffix =
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i=0 then "" else i.ToString())
            if not <| providedTys.ContainsKey newName
            then newName else findUniq prefix (i+1)
        let newName = findUniq (namePref+suffix) 0
        providedTys.Add(newName, None)
        newName

    let generateProperty propName ty (scope:UniqueNameGenerator) =
        let propertyName = scope.MakeUnique <| nicePascalName propName
        let providedField = ProvidedField("_" + propertyName.ToLower(), ty)
        let providedProperty =
            ProvidedProperty(propertyName, ty,
                GetterCode = (fun [this] -> Expr.FieldGet (this, providedField)),
                SetterCode = (fun [this;v] -> Expr.FieldSet(this, providedField, v)))
        if propName <> propertyName then
            providedProperty.AddCustomAttribute
                <| SwaggerProvider.Internal.RuntimeHelpers.getPropertyNameAttribute propName
        providedField, providedProperty

    let rec compileDefinition (tyDefName:string) =
        match definitionTys.TryGetValue tyDefName with
        | true, ty -> ty
        | false, _ ->
            match definitions.TryFind tyDefName with
            | Some(def) ->
                let tyName = tyDefName.Substring("#/definitions/".Length).Replace(".","")
                let ty = compileSchemaObject tyName def false // ?? false
                if not <| definitions.ContainsKey tyDefName
                    then definitionTys.Add(tyDefName, ty)
                ty
            | None ->
                let tys = definitionTys.Keys |> Seq.toArray
                failwithf "Unknown definition '%s' in definitionTys %A" tyDefName tys
    and compileSchemaObject tyName (schemaObj:SchemaObject) isRequired =
        match schemaObj, isRequired with
        | Boolean, true   -> typeof<bool>
        | Boolean, false  -> typeof<Option<bool>>
        | Byte, true      -> typeof<byte>
        | Byte, false     -> typeof<Option<byte>>
        | Int32, true     -> typeof<int32>
        | Int32, false    -> typeof<Option<int32>>
        | Int64, true     -> typeof<int64>
        | Int64, false    -> typeof<Option<int64>>
        | Float, true     -> typeof<float32>
        | Float, false    -> typeof<Option<float32>>
        | Double, true    -> typeof<double>
        | Double, false   -> typeof<Option<double>>
        | String, _       -> typeof<string>
        | Date, true  | DateTime, true   -> typeof<DateTime>
        | Date, false | DateTime, false  -> typeof<Option<DateTime>>
        | File, _         -> typeof<byte>.MakeArrayType(1)
        | Enum _, _       -> typeof<string> //TODO: find better type
        | Array eTy, _    -> (compileSchemaObject (uniqueName tyName "Item") eTy true).MakeArrayType()
        | Dictionary eTy,_-> typedefof<Map<string, obj>>.MakeGenericType(
                                [|typeof<string>; compileSchemaObject (uniqueName tyName "Item") eTy false|])
        | Object properties, _ ->
            if properties.Length = 0 then typeof<obj>
            else
              if isNull tyName then
                failwithf "Swagger provider does not support anonymous types: %A" schemaObj
              else
                // Register every ProvidedTypeDefinition
                match providedTys.TryGetValue tyName with
                | true, Some(ty) -> ty :> Type
                | isExist, _ ->
                    let ty = ProvidedTypeDefinition(tyName, Some typeof<obj>, IsErased = false)
                    if isExist
                    then providedTys.[tyName] <- Some(ty)
                    else providedTys.Add(tyName, Some(ty))

                    // Add default constructor
                    ty.AddMember <| ProvidedConstructor([], InvokeCode = fun _ -> <@@ () @@>)

                    // Generate fields and properties
                    let members =
                        let propsNameScope = UniqueNameGenerator()
                        properties
                        |> Array.map (fun p ->
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
                        (members |> Array.collect (fun (f,p) -> [|f :> MemberInfo; p:> MemberInfo|]) |> List.ofArray)

                    // Override `.ToString()`
                    let toStr = ProvidedMethod("ToString", [], typeof<string>, IsStaticMethod = false)
                    toStr.InvokeCode <- fun args ->
                        let this = args.[0]
                        let (pNames, pValues) =
                            members
                            |> Array.map (fun (pField, pProp) ->
                                let pValObj = Expr.FieldGet(this, pField)
                                pProp.Name, Expr.Coerce(pValObj, typeof<obj>)
                               )
                            |> Array.unzip
                        let pValuesArr = Expr.NewArray(typeof<obj>, List.ofArray pValues)
                        <@@
                            let values = (%%pValuesArr : array<obj>)
                            let rec formatValue (v:obj) =
                                if v = null then "null"
                                else
                                    let vTy = v.GetType()
                                    if vTy = typeof<string>
                                    then String.Format("\"{0}\"",v)
                                    elif vTy.IsArray
                                    then
                                        let elements =
                                            seq {
                                                for x in (v :?> System.Collections.IEnumerable) do
                                                    yield formatValue x
                                            } |> Seq.toArray
                                        String.Format("[{0}]", String.Join("; ", elements))
                                    else v.ToString()

                            let strs = values |> Array.mapi (fun i v ->
                                String.Format("{0}={1}",pNames.[i], formatValue v))
                            String.Format("{{{0}}}", String.Join("; ",strs))
                        @@>
                    toStr.SetMethodAttrs(
                        MethodAttributes.Public
                        ||| MethodAttributes.Virtual)

                    let objToStr = (typeof<obj>).GetMethod("ToString",[||])
                    ty.DefineMethodOverride(toStr, objToStr)
                    ty.AddMember <| toStr

                    ty :> Type
        | Reference path, _ -> compileDefinition path

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

    /// Default value for parameters
    member __.GetDefaultValue schemaObj =
        match schemaObj with
        | Boolean
        | Byte | Int32 | Int64
        | Float| Double 
        | Date | DateTime
           -> box <| None
        | String | Enum _
           -> box <| Unchecked.defaultof<string>
        | File | Array _ 
        | Dictionary _ | Object _
        | Reference _
           -> null

