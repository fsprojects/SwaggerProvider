namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.Schema
open Microsoft.FSharp.Quotations
open System

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerObject) =
    let definitions = Map.ofSeq schema.Definitions
    let definitionTys = System.Collections.Generic.Dictionary<_,_>()

    let providedTys = System.Collections.Generic.Dictionary<_,_>()
    let uniqueName name suffix =
        let rec findUniq prefix i =
            let newName = sprintf "%s%s" prefix (if i=0 then "" else i.ToString())
            if not <| providedTys.ContainsKey newName
            then newName else findUniq prefix (i+1)
        let newName = findUniq (name+suffix) 0
        providedTys.Add(newName, None)
        newName

    let generateProperty name ty providedField =
        let propertyName = nicePascalName name
        let property =
            ProvidedProperty(propertyName, ty,
                GetterCode = (fun [this] -> Expr.FieldGet (this, providedField)),
                SetterCode = (fun [this;v] -> Expr.FieldSet(this, providedField, v)))
        if name <> propertyName then
            property.AddCustomAttribute
                <| SwaggerProvider.Internal.RuntimeHelpers.getPropertyNameAttribute name
        property

    let rec compileDefinition (name:string) =
        match definitionTys.TryGetValue name with
        | true, ty -> ty
        | false, _ ->
            match definitions.TryFind name with
            | Some(def) ->
                let ty = compileSchemaObject name def false // ?? false
                definitionTys.Add(name, ty)
                ty
            | None ->
                let tys = definitionTys.Keys |> Seq.toArray
                failwithf "Unknown definition '%s' in definitionTys %A" name tys
    and compileSchemaObject name (schemaObj:SchemaObject) isRequired =
        match schemaObj, isRequired with
        | Boolean, true   -> typeof<bool>
        | Boolean, false  -> typeof<Option<bool>>
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
        | Array eTy, _    -> (compileSchemaObject (uniqueName name "Item") eTy true).MakeArrayType()
        | Dictionary eTy,_-> typedefof<Map<string, obj>>.MakeGenericType(
                                [|typeof<string>; compileSchemaObject (uniqueName name "Item") eTy false|])
        | Object properties, _ ->
            if isNull name then
                if properties.Length = 0 then typeof<obj>
                else failwithf "Swagger provider does not support anonymous types: %A" schemaObj
            else
                let name = name.Replace("#/definitions/", "")
                let ty = ProvidedTypeDefinition(name, Some typeof<obj>, IsErased = false)
                ty.AddMembersDelayed(fun () ->
                    [ yield ProvidedConstructor([], InvokeCode = fun _ -> <@@ () @@>) :> Reflection.MemberInfo
                      for p in properties do
                        let pTy = compileSchemaObject (uniqueName name p.Name) p.Type p.IsRequired
                        let field = ProvidedField("_" + p.Name.ToLower(), pTy)
                        yield field :> _

                        let pPr = generateProperty p.Name pTy field
                        if not <| String.IsNullOrWhiteSpace p.Description
                            then pPr.AddXmlDoc p.Description
                        yield pPr :> _ ])
                // Register every ProvidedTypeDefinition
                match providedTys.TryGetValue name with
                | true, Some(_)->
                    failwithf "This should not happened! Type '%s' was already generated" name
                | true, None -> providedTys.[name] <- Some(ty)
                | false, _ ->   providedTys.Add(name,Some(ty))
                ty :> Type
        | Reference path, _ -> compileDefinition path

    // Compiles the `definitions` part of the schema
    do  schema.Definitions
        |> Seq.iter (fun (name,_) ->
            compileDefinition name |> ignore)

    /// Compiles the definition.
    member __.GetProvidedTypes() =
        let root = ProvidedTypeDefinition("Definitions", Some typeof<obj>, IsErased = false)
        root.AddMembersDelayed (fun () ->
            List.ofSeq providedTys.Values
            |> List.choose (id))
        root

    /// Compiles the definition.
    member __.CompileTy opName tyUseSuffix ty required =
        compileSchemaObject (uniqueName opName tyUseSuffix) ty required
