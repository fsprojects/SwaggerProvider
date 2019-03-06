module SwaggerProvider.Tests

open SwaggerProvider.Internal.Compilers
open Expecto
open System
open System.IO

let parserTestBody (path:string) = async {
    let! schemaStr = 
        match Uri.TryCreate(path, UriKind.Absolute) with
        | true, uri when path.IndexOf("http") >=0  -> 
            try
                APIsGuru.httpClient.GetStringAsync(uri)
                |> Async.AwaitTask
            with e ->
                Tests.skiptestf "Network issue. Cannot download %s" e.Message
        | _ when File.Exists(path) ->
            async { return File.ReadAllText path}
        | _ -> 
            failwithf "Cannot find schema '%s'" path

    if not <| System.String.IsNullOrEmpty(schemaStr) then
        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

        let (schema, diagnostic) = openApiReader.Read(schemaStr)
        // TODO: Should we ignore `diagnostic` or fails?
        //if diagnostic.Errors.Count > 0 then
        //    failwithf "Schema parse errors: %s"
        //        (diagnostic.Errors
        //         |> Seq.map (fun e -> e.Message)
        //         |> String.concat ";")

        try
            let defCompiler = DefinitionCompiler(schema, false)
            let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
            opCompiler.CompileProvidedClients(defCompiler.Namespace)
            ignore <| defCompiler.Namespace.GetProvidedTypes()
        with
        | e when e.Message.IndexOf("not supported yet") >= 0 -> ()
    }

[<Tests>]
let petStoreTests =
    let root = Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
               |> Path.GetFullPath
    Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
    |> List.ofArray
    |> List.filter (fun s -> s.IndexOf("ignored") < 0)
    |> List.map (fun file ->
        let path = Path.GetFullPath(file).Substring(root.Length)
        testCaseAsync
            (sprintf "Parse schema %s" path)
            (parserTestBody file)
       )
    |> testList "All/Schema"

(*
[<Tests>]
let parseJsonSchemaTests =
    APIsGuru.Schemas.Value
    |> List.ofArray
    |> List.map (fun url ->
        testCaseAsync
            (sprintf "Parse schema %s" url)
            (parserTestBody url)
       )
    |> testList "Integration/Schema"
*)

