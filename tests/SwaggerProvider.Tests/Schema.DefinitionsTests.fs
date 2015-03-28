module SwaggerProvider.DefinitionsTests

open SwaggerProvider.Internal.Schema
open FSharp.Data
open NUnit.Framework


let shouldBeEqual (expected:DefinitionPropertyType) s =
    let json =JsonValue.Parse(s)
    let result = DefinitionPropertyType.Parse(json)
    if expected <> result then
        failwithf "Expected %A, but received %A" expected result

[<Test>]
let ``parse boolean`` () =
    """{
		"type" : "boolean"
	}"""
    |> shouldBeEqual Boolean

[<Test>]
let ``parse int32`` () =
    """{
	    "type" : "integer",
	    "format" : "int32",
        "description" : "User Status"
    }"""
    |> shouldBeEqual Int32

[<Test>]
let ``parse int64`` () =
    """{
	    "type" : "integer",
	    "format" : "int64"
    }"""
    |> shouldBeEqual Int64

[<Test>]
let ``parse float`` () =
    """{
	    "type" : "number",
	    "format" : "float"
    }"""
    |> shouldBeEqual Float

[<Test>]
let ``parse double`` () =
    """{
	    "type" : "number",
	    "format" : "double"
    }"""
    |> shouldBeEqual Double

[<Test>]
let ``parse string`` () =
    """{"type" : "string"}"""
    |> shouldBeEqual String

[<Test>]
let ``parse date-time`` () =
    """{
		"type" : "string",
		"format" : "date-time"
	}"""
    |> shouldBeEqual DateTime

[<Test>]
let ``parse date`` () =
    """{
		"type" : "string",
		"format" : "date"
	}"""
    |> shouldBeEqual Date

[<Test>]
let ``parse enum`` () =
    """{
		"type" : "string",
		"description" : "pet status in the store",
		"enum" : ["available", "pending", "sold"]
	}"""
    |> shouldBeEqual (Enum [|"available"; "pending"; "sold"|])

[<Test>]
let ``parse definition reference`` () =
    """{"$ref" : "#/definitions/Tag"}"""
    |> shouldBeEqual (Definition "#/definitions/Tag")

[<Test>]
let ``parse array of definitions`` () =
    """{
		"type" : "array",
		"xml" : {
			"name" : "tag",
			"wrapped" : true
		},
		"items" : {
			"$ref" : "#/definitions/Tag"
		}
	}"""
    |> shouldBeEqual (Array (Definition "#/definitions/Tag"))

[<Test>]
let ``parse array of string`` () =
    """{
		"type" : "array",
		"xml" : {
			"name" : "photoUrl",
			"wrapped" : true
		},
		"items" : {
			"type" : "string"
		}
	}"""
    |> shouldBeEqual (Array String)
