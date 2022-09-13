module SwaggerProvider.Tests.v2.Schema_PathsTests

open SwaggerProvider.Internal.v2.Parser.Schema
open SwaggerProvider.Internal.v2.Parser
open FsUnitTyped
open Xunit

let shouldBeEqualToTag (expected: TagObject) content =
    content
    |> SwaggerParser.parseJson
    |> Parsers.parseTagObject
    |> shouldEqual expected

[<Fact>]
let ``parse simple tag``() =
    """{
        "name" : "store",
        "description" : "Operations about user"
    }"""
    |> shouldBeEqualToTag
        {
            Name = "store"
            Description = "Operations about user"
        }

[<Fact>]
let ``parse partial tag``() =
    """{
        "name" : "store"
    }"""
    |> shouldBeEqualToTag
        {
            Name = "store"
            Description = System.String.Empty
        }

[<Fact>]
let ``parse complex tag``() =
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
