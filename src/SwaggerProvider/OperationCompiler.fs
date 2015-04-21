namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.Schema
open System

type OperationCompiler (schema:SwaggerSchema, defCompiler:DefinitionCompiler) =

    let operationGroups =
        schema.Operations
        |> Seq.groupBy (fun op->
            if op.Tags.Length > 0
            then op.Tags.[0] else "Root")
        |> Seq.toList

    let compileOperation (op:OperationObject) =
        let parameters =
            [let required, optional = op.Parameters |> Array.partition (fun x->x.Required)
             for x in Array.append required optional ->
                ProvidedParameter(x.Name, defCompiler.CompileTy x.Type)]
        let retTy =
            let okResponse =
                op.Responses |> Array.tryFind (fun x->
                    (x.StatusCode.IsSome && x.StatusCode.Value = 200) || x.StatusCode.IsNone)
            match okResponse with
            | Some resp ->
                match resp.Schema with
                | None -> typeof<unit>
                | Some ty -> defCompiler.CompileTy ty
            | None -> typeof<unit>
        let m = ProvidedMethod(nicePascalName op.OperationId, parameters, retTy, IsStaticMethod = true)
        m.AddXmlDoc(op.Summary)
        m.InvokeCode <- fun args -> <@@ raise (NotImplementedException()) @@>
        m

    member __.Compile() =
        operationGroups
        |> List.map (fun (tag, operations) ->
            let ty = ProvidedTypeDefinition(nicePascalName tag, Some typeof<obj>, IsErased = false)
            operations
            |> Seq.map compileOperation
            |> Seq.toList
            |> ty.AddMembers
            ty
        )