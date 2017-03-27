namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal
open SwaggerProvider.Internal.Schema
open Microsoft.FSharp.Quotations
open System

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

    let getPropertyNameAttribute name =
        { new System.Reflection.CustomAttributeData() with
            member __.Constructor =  typeof<Newtonsoft.Json.JsonPropertyAttribute>.GetConstructor([|typeof<string>|])
            member __.ConstructorArguments = [|Reflection.CustomAttributeTypedArgument(typeof<string>, name)|] :> System.Collections.Generic.IList<_>
            member __.NamedArguments = [||] :> System.Collections.Generic.IList<_> }

    let generateProperty propName ty (scope:UniqueNameGenerator) =
        let propertyName = scope.MakeUnique <| nicePascalName propName
        let providedField = ProvidedField("_" + propertyName.ToLower(), ty)
        let providedProperty =
            ProvidedProperty(propertyName, ty,
                GetterCode = (fun [this] -> Expr.FieldGet (this, providedField)),
                SetterCode = (fun [this;v] -> Expr.FieldSet(this, providedField, v)))
        if propName <> propertyName then
            providedProperty.AddCustomAttribute
                <| getPropertyNameAttribute propName
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

                    ty.AddMember <| ProvidedConstructor([], InvokeCode = fun _ -> <@@ () @@>)
                    let propsNameScope = UniqueNameGenerator()
                    for p in properties do
                        if String.IsNullOrEmpty(p.Name)
                            then failwithf "Property cannot be created with empty name. Obj name:%A; ObjSchema:%A" tyName schemaObj

                        let pTy = compileSchemaObject (uniqueName tyName (nicePascalName p.Name)) p.Type p.IsRequired
                        let (pField, pProp) = generateProperty p.Name pTy propsNameScope
                        if not <| String.IsNullOrWhiteSpace p.Description
                            then pProp.AddXmlDoc p.Description

                        ty.AddMember <| pField
                        ty.AddMember <| pProp

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
