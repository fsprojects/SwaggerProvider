module SwaggerProvider.PathsTests

open Swagger.Parser.Schema
open Swagger.Parser
open Expecto

let shouldBeEqualToTag (expected:TagObject) content =
    content
    |> SwaggerParser.parseJson
    |> Parsers.parseTagObject
    |> fun actual ->
        Expect.equal actual expected "parse tags"

[<Tests>]
let jsonSpecTests =
  testList "All/Parse/PathsTags" [

    testCase "parse simple tag" <| fun _ ->
        """{
            "name" : "store",
            "description" : "Operations about user"
        }"""
        |> shouldBeEqualToTag
            {
                Name = "store"
                Description = "Operations about user"
            }

    testCase "parse partial tag" <| fun _ ->
        """{
            "name" : "store"
        }"""
        |> shouldBeEqualToTag
            {
                Name = "store"
                Description = System.String.Empty
            }

    testCase "parse complex tag" <| fun _ ->
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
  ]