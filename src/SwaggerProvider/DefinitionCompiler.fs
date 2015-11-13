namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.Schema
open Microsoft.FSharp.Quotations
open System

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerObject) =
    let definitions = Map.ofSeq schema.Definitions
    let compiledTys = System.Collections.Generic.Dictionary<_,_>()

    let generateProperty name ty providedField =
         ProvidedProperty(nicePascalName name, ty, GetterCode = (fun [this] -> Expr.FieldGet (this, providedField)),
                                                   SetterCode = (fun [this;v] -> Expr.FieldSet(this, providedField, v)))

    let rec compileDefinition (name:string) =
        match compiledTys.TryGetValue name with
        | true, ty -> ty
        | false, _ ->
            match definitions.TryFind name with
            | Some(def) ->
                let ty = compileSchemaObject name def false // ?? false
                compiledTys.Add(name, ty)
                ty
            | None ->
                let tys = compiledTys.Keys |> Seq.toArray
                failwithf "Unknown definition '%s' in compiledTys %A" name tys
    and compileSchemaObject name (schemaObj:SchemaObject) isRequired =
        match schemaObj, isRequired with
        | Boolean, true   -> typeof<bool>
        | Boolean, false  -> typeof<Option<bool>>
        | Int32, true     -> typeof<int32>
        | Int32, false    -> typeof<Option<int32>>
        | Int64, true     -> typeof<int64>
        | Int64, false    -> typeof<Option<int64>>
        | Float, true     -> typeof<float>
        | Float, false    -> typeof<Option<float>>
        | Double, true    -> typeof<double>
        | Double, false   -> typeof<Option<double>>
        | String, _       -> typeof<string>
        | Date, true  | DateTime, true   -> typeof<DateTime>
        | Date, false | DateTime, false  -> typeof<Option<DateTime>>
        | File, _         -> typeof<byte>.MakeArrayType(1)
        | Enum _, _       -> typeof<string> //TODO: find better type
        | Array eTy, _    -> (compileSchemaObject null eTy true).MakeArrayType()
        | Dictionary eTy,_-> typedefof<Map<string, obj>>.MakeGenericType([|typeof<string>; compileSchemaObject null eTy false|])
        | Object properties, _ ->
            if name = null then
                if properties.Length = 0
                then typeof<obj>
                else failwith "This should not happened"
            else
                let name =name.Substring("#/definitions/".Length);
                let ty = ProvidedTypeDefinition(nicePascalName name, Some typeof<obj>, IsErased = false)
                ty.AddMemberDelayed(fun () -> ProvidedConstructor([],
                                               InvokeCode = fun args -> <@@ () @@>))
                for p in properties do
                    let pTy = compileSchemaObject null p.Type p.IsRequired
                    let field = ProvidedField("_" + p.Name.ToLower(), pTy)
                    ty.AddMember field
                    let pPr = generateProperty p.Name pTy field
                    if not <| String.IsNullOrWhiteSpace p.Description
                        then pPr.AddXmlDoc p.Description
                    ty.AddMember pPr
                ty :> Type
        | Reference path, _ -> compileDefinition path


    /// Compiles the definition.
    member __.Compile() =
        let root = ProvidedTypeDefinition("Definitions", Some typeof<obj>, IsErased = false)
        schema.Definitions
        |> Seq.iter (fun (name,_) ->
            compileDefinition name |> ignore)
        for pTy in compiledTys.Values do
            root.AddMember pTy
        root

    /// Compiles the definition.
    member __.CompileTy ty required =
        compileSchemaObject null ty required
