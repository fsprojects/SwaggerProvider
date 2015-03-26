module SwaggerProvider.Tests

open SwaggerProvider.Schema
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