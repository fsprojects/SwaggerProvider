module SwaggerProvider.Tests.v3

open Expecto
open System
open System.IO

module V2 =
    open SwaggerProvider.Internal.v2.Compilers
    open SwaggerProvider.Internal.v2.Parser
    let testSchema schemaStr =
        let schema = SwaggerParser.parseSchema schemaStr

        let defCompiler = DefinitionCompiler(schema, false)
        let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
        opCompiler.CompileProvidedClients(defCompiler.Namespace)
        ignore <|  defCompiler.Namespace.GetProvidedTypes()

module V3 =
    open SwaggerProvider.Internal.v3.Compilers
    let testSchema schemaStr =
        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

        let (schema, diagnostic) = openApiReader.Read(schemaStr)
    (*        if diagnostic.Errors.Count > 0 then
               failwithf "Schema parse errors:\n- %s"
                   (diagnostic.Errors
                    |> Seq.map (fun e -> e.Message)
                    |> String.concat ";\n- ")*)

        try
            let defCompiler = DefinitionCompiler(schema, false)
            let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
            opCompiler.CompileProvidedClients(defCompiler.Namespace)
            ignore <| defCompiler.Namespace.GetProvidedTypes()
        with
        | e when e.Message.IndexOf("not supported yet") >= 0 -> ()

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
        if path.IndexOf("v2") >= 0
        then V2.testSchema schemaStr
        else V3.testSchema schemaStr
    }

[<Tests>]
let knownSchemaTests =
    let root = Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
               |> Path.GetFullPath
    Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
    |> List.ofArray
    |> List.filter (fun s -> s.IndexOf("ignored") < 0)
    |> List.map (fun file ->
        let path = Path.GetFullPath(file).Substring(root.Length)
        testCaseAsync
            (sprintf "Parse%s" path)
            (parserTestBody file)
       )
    |> testList "All/Schema"

[<Tests>]
let petstoreTest =
    testCaseAsync "Parse PetStore"
        (parserTestBody (__SOURCE_DIRECTORY__ + "/../SwaggerProvider.ProviderTests/Schemas/v2/petstore.json"))

(*
[<Tests>]
let parseJsonSchemaTests =
    APIsGuru.Schemas.Value
    |> List.ofArray
    |> List.map (fun url ->
        testCaseAsync
            (sprintf "Parse %s" url)
            (parserTestBody url)
       )
    |> testList "Integration/Schema"
*)

