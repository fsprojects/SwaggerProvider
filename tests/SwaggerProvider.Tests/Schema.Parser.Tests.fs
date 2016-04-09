module SwaggerProvider.Tests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open SwaggerProvider.Internal.Compilers
open FSharp.Data
open NUnit.Framework
open FsUnit
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
    schema.Definitions |> should haveLength 6

    schema.Info
    |> should equal {
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
    schema.Definitions |> should haveLength 6

    let schemaOnline =
        "http://petstore.swagger.io/v2/swagger.json"
        |> Http.RequestString
        |> JsonValue.Parse
        |> JsonNodeAdapter
        |> Parser.parseSwaggerObject

    schemaOnline.BasePath |> should equal schema.BasePath
    schemaOnline.Host |> should equal schema.Host
    schemaOnline.Info |> should equal schema.Info
    schemaOnline.Schemes |> should equal schema.Schemes
    schemaOnline.Tags |> should equal schema.Tags
    schemaOnline.Definitions |> should equal schema.Definitions
    schemaOnline.Paths |> should equal schema.Paths
    schemaOnline |> should equal schema


// Test that provider can parse real-word Swagger 2.0 schemas
// https://github.com/APIs-guru/api-models/blob/master/API.md
type ApisGuru = FSharp.Data.JsonProvider<"http://apis-guru.github.io/api-models/api/v1/list.json">

let toTestCase (url:string) =
    TestCaseData(url).SetName(sprintf "Parse schema %s" url)

let getApisGuruSchemas propertyName =
    ApisGuru.GetSample().JsonValue.Properties()
    |> Array.choose (fun (name, obj)->
        obj.TryGetProperty("versions")
        |> Option.bind (fun v->
            v.Properties()
            |> Array.choose (fun (_,x)-> x.TryGetProperty(propertyName))
            |> Some
        )
       )
    |> Array.concat
    |> Array.map (fun x->x.AsString())
    |> Array.map toTestCase

let ApisGuruJsonSchemaUrls = getApisGuruSchemas "swaggerUrl"
let ApisGuruYamlSchemaUrls = getApisGuruSchemas "swaggerYamlUrl"

let ManualSchemaUrls =
    [|"http://netflix.github.io/genie/docs/rest/swagger.json"
      //"https://www.expedia.com/static/mobile/swaggerui/swagger.json" // This schema is incorrect
      "https://graphhopper.com/api/1/vrp/swagger.json"|]
    |> Array.map toTestCase

let SchemaUrls =
    Array.concat [ManualSchemaUrls; ApisGuruJsonSchemaUrls]

let SchemasWithZeroPathes =
    [
     "https://apis-guru.github.io/api-models/googleapis.com/iam/v1alpha1/swagger.json"
     "https://apis-guru.github.io/api-models/googleapis.com/iam/v1alpha1/swagger.yaml"
    ] |> Set.ofList

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

        if Set.contains url SchemasWithZeroPathes
        then schema.Paths.Length |> should equal 0
        else schema.Paths.Length + schema.Definitions.Length |> should be (greaterThan 0)

        //Number of generated types may be less than number of type definition in schema
        //TODO: Check if TPs are able to generate aliases like `type RandomInd = int`
        let defCompiler = DefinitionCompiler(schema)
        let opCompiler = OperationCompiler(schema, defCompiler, [])
        ignore <| opCompiler.Compile(url)
        ignore <| defCompiler.GetProvidedTypes()


[<Test; TestCaseSource("SchemaUrls")>]
let ``Parse Json Schema`` (url:string) =
    parserTestBody (JsonValue.Parse >> JsonNodeAdapter) url

[<Test; TestCaseSource("ApisGuruYamlSchemaUrls")>]
let ``Parse Yaml Schema`` url =
    parserTestBody (SwaggerProvider.YamlParser.Parse >> YamlNodeAdapter) url