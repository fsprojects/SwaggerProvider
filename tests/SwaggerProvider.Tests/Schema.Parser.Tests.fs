module SwaggerProvider.Tests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open SwaggerProvider.Internal.Compilers
open FSharp.Data
open NUnit.Framework
open FsUnitTyped
open System.IO

[<SetUpFixture>]
type SetUpCurrentDirectory() =
    [<OneTimeSetUp>]
    member __.OneTimeSetUp() =
        let path = typeof<SetUpCurrentDirectory>.Assembly.Location
        let dir = Path.GetDirectoryName(path)
        System.Environment.CurrentDirectory <- dir

[<Test>]
let ``Schema parse of PetStore.Swagger.json sample`` () =
    let schema =
        "Schemas/PetStore.Swagger.json"
        |> File.ReadAllText
        |> JsonValue.Parse
        |> JsonNodeAdapter
        |> Parser.parseSwaggerObject
    schema.Definitions |> shouldHaveLength 6

    schema.Info
    |> shouldEqual {
        Title = "Swagger Petstore"
        Version = "1.0.0"
        Description = "This is a sample server Petstore server.  You can find out more about Swagger at [http://swagger.io](http://swagger.io) or on [irc.freenode.net, #swagger](http://swagger.io/irc/).  For this sample, you can use the api key `special-key` to test the authorization filters."
    }

[<Test>]
let ``Schema parse of PetStore.Swagger.json sample (online)`` () =
    let schema =
        "Schemas/PetStore.Swagger.json"
        |> File.ReadAllText
        |> JsonValue.Parse
        |> JsonNodeAdapter
        |> Parser.parseSwaggerObject
    schema.Definitions |> shouldHaveLength 6

    let schemaOnline =
        "http://petstore.swagger.io/v2/swagger.json"
        |> Http.RequestString
        |> JsonValue.Parse
        |> JsonNodeAdapter
        |> Parser.parseSwaggerObject

    schemaOnline.BasePath |> shouldEqual schema.BasePath
    schemaOnline.Host |> shouldEqual schema.Host
    schemaOnline.Info |> shouldEqual schema.Info
    schemaOnline.Schemes |> shouldEqual schema.Schemes
    schemaOnline.Tags |> shouldEqual schema.Tags
    schemaOnline.Definitions |> shouldEqual schema.Definitions
    schemaOnline.Paths |> shouldEqual schema.Paths
    schemaOnline |> shouldEqual schema

let parserTestBody formatParser (url:string) =
    let schemaStr =
        try
            if url.StartsWith("http")
            then Http.RequestString url
            else File.ReadAllText url
        with
        | :? System.Net.WebException ->
            printfn "Schema is unaccessible %s" url
            ""
    if not <| System.String.IsNullOrEmpty(schemaStr) then
        let schema = formatParser schemaStr
                     |> Parsers.Parser.parseSwaggerObject

        schema.Paths.Length + schema.Definitions.Length |> shouldBeGreaterThan 0

        //Number of generated types may be less than number of type definition in schema
        //TODO: Check if TPs are able to generate aliases like `type RandomInd = int`
        let defCompiler = DefinitionCompiler(schema)
        let opCompiler = OperationCompiler(schema, defCompiler)
        ignore <| opCompiler.CompilePaths(false)
        ignore <| defCompiler.GetProvidedTypes()


let toTestCase (url:string) =
    TestCaseData(url).SetName(sprintf "Parse schema %s" url)

let private schemasFromTPTests =
    let folder = Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
    Directory.GetFiles(folder)
let JsonSchemasSource = 
    Array.concat [schemasFromTPTests; APIsGuru.JsonSchemas]
    |> Array.map toTestCase

[<Test; TestCaseSource("JsonSchemasSource"); Category("Integration")>]
let ``Parse Json Schema`` (url:string) =
    parserTestBody (JsonValue.Parse >> JsonNodeAdapter) url

let YamlSchemasSource = APIsGuru.YamlSchemas |> Array.map toTestCase
[<Test; TestCaseSource("YamlSchemasSource"); Category("Integration")>]
let ``Parse Yaml Schema`` url =
    parserTestBody (SwaggerProvider.YamlParser.Parse >> YamlNodeAdapter) url