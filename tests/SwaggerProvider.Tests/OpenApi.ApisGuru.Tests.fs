module SwaggerProvider.OpenApi.Tests

open Expecto
open System
open System.IO
open System.Net.Http
open Microsoft.OpenApi.Readers
open Microsoft.OpenApi

let openApiTestBody (url:string) =
    let stream = 
        match Uri.TryCreate(url, UriKind.Absolute) with
        | true, uri when url.IndexOf("http") >=0 -> 
            let client = new HttpClient()
            client.GetStreamAsync(uri)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        | _ when File.Exists(url) ->
            new FileStream(url, FileMode.Open) :> Stream
        | _ -> 
            failwithf "Cannot find schema '%s'" url

    let (schema, diagnostic) = 
        OpenApiStreamReader().Read(stream)

    Expect.equal (OpenApiSpecVersion.OpenApi2_0) (diagnostic.SpecificationVersion) "Expect v2.0 schemas"
    Expect.isGreaterThan
        (schema.Paths.Count + schema.Components.Schemas.Count)
        0 "schema should provide type or operation definitions"

    //Number of generated types may be less than number of type definition in schema
    //let defCompiler = DefinitionCompiler(schema, false)
    //let opCompiler = OperationCompiler(schema, defCompiler, true, false)
    //opCompiler.CompileProvidedClients(defCompiler.Namespace)
    //ignore <| defCompiler.Namespace.GetProvidedTypes()

//[<Tests>]
let parseJsonSchemaOpenApiTests =
    SwaggerProvider.Tests.JsonSchemasSource
    |> List.map (fun url ->
        testCase
            (sprintf "Parse schema %s" url)
            (fun _ -> openApiTestBody url)
       )
    |> testList "All/Schema Json Open.API Schemas"