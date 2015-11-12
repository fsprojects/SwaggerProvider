module SwaggerProvider.PathsTests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open FSharp.Data
open NUnit.Framework
open FsUnit

let shouldBeEqualToTag (expected:TagObject) content =
    content
    |> JsonValue.Parse
    |> JsonParser.parseTagObject
    |> should equal expected

[<Test>]
let ``parse simple tag`` () =
    """{
        "name" : "store",
        "description" : "Operations about user"
    }"""
    |> shouldBeEqualToTag
        {
            Name = "store"
            Description = "Operations about user"
        }

[<Test>]
let ``parse partial tag`` () =
    """{
        "name" : "store"
    }"""
    |> shouldBeEqualToTag
        {
            Name = "store"
            Description = System.String.Empty
        }

[<Test>]
let ``parse complex tag`` () =
    """{
        "name" : "user",
        "description" : "Access to Petstore orders",
        "externalDocs" : {
            "description" : "Find out more about our store",
            "url" : "http://swagger.io"
        }
    }"""
    |> shouldBeEqualToTag
        {
            Name = "user"
            Description = "Access to Petstore orders"
        }