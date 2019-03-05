module SwaggerProvider.Tests

open SwaggerProvider.Internal.Compilers
open Expecto
open System
open System.IO

let parserTestBody (path:string) =
    let schemaStr = 
        match Uri.TryCreate(path, UriKind.Absolute) with
        | true, uri when path.IndexOf("http") >=0  -> 
            use client = new Net.WebClient()
            try
                client.DownloadString(uri)
            with e ->
                Tests.skiptestf "Network issue. Cannot download %s" e.Message
        | _ when File.Exists(path) ->
            File.ReadAllText path
        | _ -> 
            failwithf "Cannot find schema '%s'" path

    if not <| System.String.IsNullOrEmpty(schemaStr) then
        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

        let (schema, diagnostic) = openApiReader.Read(schemaStr)
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

[<Tests>]
let petStoreTests =
    let folder = Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
    Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
    |> List.ofArray
    |> List.filter (fun s -> s.IndexOf("ignored") < 0)
    |> List.map (fun file ->
        testCase
            (sprintf "Parse schema %s" file)
            (fun _ -> parserTestBody file)
       )
    |> testList "All/Schema"

(*

[<Tests>]
let parseJsonSchemaTests =
    APIsGuru.Schemas.Value
    |> List.ofArray
    |> List.map (fun url ->
        testCase
            (sprintf "Parse schema %s" url)
            (fun _ -> parserTestBody url)
       )
    |> testList "Integration/Schema"

*)
