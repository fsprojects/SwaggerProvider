module SwaggerProvider.Tests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open SwaggerProvider.Internal.Compilers
open FSharp.Data
open Expecto
open System.IO

type ThisAssemblyPointer() = class end
let root =
    typeof<ThisAssemblyPointer>.Assembly.Location
    |> Path.GetDirectoryName

let (!) b = Path.Combine(root,b)

[<Tests>]
let petStoreTests =
  testList "All/Schema PetStore" [
    testCase "Schema parse of PetStore.Swagger.json sample (offline)" <| fun _ ->
        let schema =
            !"Schemas/PetStore.Swagger.json"
            |> File.ReadAllText
            |> JsonValue.Parse
            |> JsonNodeAdapter
            |> Parser.parseSwaggerObject
        Expect.equal
            (schema.Definitions.Length)
            6 "only 6 objects in PetStore"

        let expectedInfo =
            {
                Title = "Swagger Petstore"
                Version = "1.0.0"
                Description = "This is a sample server Petstore server.  You can find out more about Swagger at [http://swagger.io](http://swagger.io) or on [irc.freenode.net, #swagger](http://swagger.io/irc/).  For this sample, you can use the api key `special-key` to test the authorization filters."
            }
        Expect.equal (schema.Info) expectedInfo "PetStore schema info"

    testCase "Schema parse of PetStore.Swagger.json sample (online)" <| fun _ ->
        let schema =
            !"Schemas/PetStore.Swagger.json"
            |> File.ReadAllText
            |> JsonValue.Parse
            |> JsonNodeAdapter
            |> Parser.parseSwaggerObject
        Expect.equal
            (schema.Definitions.Length)
            6 "only 6 objects in PetStore"

        let schemaOnline =
            "http://petstore.swagger.io/v2/swagger.json"
            |> Http.RequestString
            |> JsonValue.Parse
            |> JsonNodeAdapter
            |> Parser.parseSwaggerObject

        Expect.equal schemaOnline.BasePath schema.BasePath "same BasePath"
        Expect.equal schemaOnline.Host schema.Host "same Host"
        Expect.equal schemaOnline.Info schema.Info "same Info"
        Expect.equal schemaOnline.Schemes schema.Schemes "same allowed schemes"
        Expect.equal schemaOnline.Tags schema.Tags "same tags"
        Expect.equal schemaOnline.Definitions schema.Definitions "same object definitions"
        Expect.equal schemaOnline.Paths schema.Paths "same paths"
        Expect.equal schemaOnline schema "same schema objects"

    testCase "Schema parse of clarify.io.1.3.0.json sample (offline)" <| fun _ ->
        let schema =
            !"Schemas/clarify.io.1.3.0.json"
            |> File.ReadAllText
            |> JsonValue.Parse
            |> JsonNodeAdapter
            |> Parser.parseSwaggerObject
        Expect.equal
            (schema.Definitions.Length)
            21 "schema should have definitions of 21 objects"

    ptestCase "Schema parse of rebilly.com.2.1.json sample (offline)" <| fun _ ->
        let schema =
            !"Schemas/rebilly.com.2.1.json"
            |> File.ReadAllText
            |> JsonValue.Parse
            |> JsonNodeAdapter
            |> Parser.parseSwaggerObject
        Expect.equal
            (schema.Definitions.Length)
            6 "only 6 objects in PetStore"

  ]

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

        Expect.isGreaterThan
            (schema.Paths.Length + schema.Definitions.Length)
            0 "schema should provide type or operation definitions"

        //Number of generated types may be less than number of type definition in schema
        //TODO: Check if TPs are able to generate aliases like `type RandomInd = int`
        let defCompiler = DefinitionCompiler(schema)
        let opCompiler = OperationCompiler(schema, defCompiler)
        ignore <| opCompiler.CompilePaths(false)
        ignore <| defCompiler.GetProvidedTypes()


let private schemasFromTPTests =
    let folder = Path.Combine(__SOURCE_DIRECTORY__, "../SwaggerProvider.ProviderTests/Schemas")
    Directory.GetFiles(folder)
let JsonSchemasSource =
    Array.concat [schemasFromTPTests; APIsGuru.JsonSchemas] |> List.ofArray
let YamlSchemasSource =
    APIsGuru.YamlSchemas |> List.ofArray

[<Tests>]
let parseJsonSchemaTests =
    JsonSchemasSource
    |> List.map (fun url ->
        testCase
            (sprintf "Parse schema %s" url)
            (fun _ -> parserTestBody (JsonValue.Parse >> JsonNodeAdapter) url)
       )
    |> testList "Integration/Schema Json Schemas"

[<Tests>]
let parseYamlSchemaTests =
    YamlSchemasSource
    |> List.map (fun url ->
        testCase
            (sprintf "Parse schema %s" url)
            (fun _ -> parserTestBody (YamlParser.Parse >> YamlNodeAdapter) url)
       )
    |> testList "Integration/Schema Yaml Schemas"
