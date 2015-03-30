module SwaggerProvider.PathsTests

open SwaggerProvider.Internal.Schema
open FSharp.Data
open NUnit.Framework

let shouldBeEqualToTag (expected:TagObject) s =
    let json =JsonValue.Parse(s)
    let result = TagObject.Parse(json)
    if expected <> result then
        failwithf "Expected %A, but received %A" expected result

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