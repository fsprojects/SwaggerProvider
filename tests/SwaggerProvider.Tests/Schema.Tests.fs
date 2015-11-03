module SwaggerProvider.Tests

open SwaggerProvider.Internal.Schema
open FSharp.Data
open NUnit.Framework
open System.IO

[<Test>]
let ``Schema parse of PetStore.Swagger.json sample`` () =
    let schema =
        "Schemas/PetStore.Swagger.json"
        |> File.ReadAllText
        |> JsonValue.Parse
        |> SwaggerSchema.Parse
    Assert.AreEqual(6, schema.Definitions.Length)

    let expectedInfo = {
        Title = "Swagger Petstore"
        Version = "1.0.0"
        Description = "This is a sample server Petstore server.  You can find out more about Swagger at [http://swagger.io](http://swagger.io) or on [irc.freenode.net, #swagger](http://swagger.io/irc/).  For this sample, you can use the api key `special-key` to test the authorization filters."
    }
    if schema.Info <> expectedInfo
        then Assert.Fail (sprintf "\n%A \n<> \n%A" schema.Info expectedInfo)
