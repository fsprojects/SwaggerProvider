module SwaggerProvider.Tests.v3

open Xunit
open FsUnitTyped
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
        ignore <| defCompiler.Namespace.GetProvidedTypes()

module V3 =
    open SwaggerProvider.Internal.v3.Compilers

    let testSchema schemaStr =
        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

        let schema, diagnostic = openApiReader.Read(schemaStr)
        (*        if diagnostic.Errors.Count > 0 then
               failwithf "Schema parse errors:\n- %s"
                   (diagnostic.Errors
                    |> Seq.map (fun e -> e.Message)
                    |> String.concat ";\n- ")*)
        try
            let defCompiler = DefinitionCompiler(schema, false)
            let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
            opCompiler.CompileProvidedClients(defCompiler.Namespace)
            defCompiler.Namespace.GetProvidedTypes()
        with e when e.Message.IndexOf("not supported yet") >= 0 ->
            List.Empty

let parserTestBody(path: string) =
    task {
        let! schemaStr =
            match Uri.TryCreate(path, UriKind.Absolute) with
            | true, uri when path.IndexOf("http") >= 0 -> APIsGuru.httpClient.GetStringAsync(uri)
            | _ when File.Exists(path) -> File.ReadAllTextAsync path
            | _ -> failwithf $"Cannot find schema '%s{path}'"

        if not <| String.IsNullOrEmpty(schemaStr) then
            if path.IndexOf("v2") >= 0 then
                V2.testSchema schemaStr
            else
                V3.testSchema schemaStr |> ignore
    }

let rootFolder =
    Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
    |> Path.GetFullPath

let allSchemas =
    Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories)
    |> List.ofArray
    |> List.map(fun s -> Path.GetRelativePath(rootFolder, s))

let knownSchemaPaths =
    allSchemas
    |> List.filter(fun s -> s.IndexOf("unsupported") < 0)
    |> List.map(fun s -> [| box s |])

[<Theory; MemberData(nameof(knownSchemaPaths))>]
let Parse file =
    let file = Path.Combine(rootFolder, file)
    parserTestBody file

let unsupportedSchemaPaths =
    allSchemas
    |> List.filter(fun s -> s.IndexOf("unsupported") > 0)
    |> List.map(fun s -> [| box s |])

[<Theory(Skip = "no samples"); MemberData(nameof(unsupportedSchemaPaths))>]
let ``Fail to parse`` file =
    let file = Path.Combine(rootFolder, file)
    shouldFail(fun () -> parserTestBody file |> Async.AwaitTask |> Async.RunSynchronously)


[<Fact>]
let ``Parse PetStore``() =
    parserTestBody(
        __SOURCE_DIRECTORY__
        + "/../SwaggerProvider.ProviderTests/Schemas/v2/petstore.json"
    )

[<Fact>]
let ``Add definition for schema with only allOf properties``() =
    let definitions =
        __SOURCE_DIRECTORY__
        + "/../SwaggerProvider.ProviderTests/Schemas/v3/issue255.yaml"
        |> File.ReadAllText
        |> V3.testSchema

    definitions |> shouldHaveLength 1
    definitions[0].GetDeclaredProperty("FirstName") |> shouldNotEqual null

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
