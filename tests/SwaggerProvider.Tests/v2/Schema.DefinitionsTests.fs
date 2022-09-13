module SwaggerProvider.Tests.v2.Schema_DefinitionsTests

open SwaggerProvider.Internal.v2.Parser.Schema
open SwaggerProvider.Internal.v2.Parser
open FsUnitTyped
open Xunit

let shouldBeEqual (expected: SchemaObject) content =
    content
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual expected

[<Fact>]
let ``parse boolean``() =
    """{
        "type" : "boolean"
    }"""
    |> shouldBeEqual Boolean

[<Fact>]
let ``parse int32``() =
    """{
        "type" : "integer",
        "format" : "int32",
        "description" : "User Status"
    }"""
    |> shouldBeEqual Int32

[<Fact>]
let ``parse int64``() =
    """{
        "type" : "integer",
        "format" : "int64"
    }"""
    |> shouldBeEqual Int64

[<Fact>]
let ``parse float``() =
    """{
        "type" : "number",
        "format" : "float"
    }"""
    |> shouldBeEqual Float

[<Fact>]
let ``parse double``() =
    """{
        "type" : "number",
        "format" : "double"
    }"""
    |> shouldBeEqual Double

[<Fact>]
let ``parse string``() =
    """{"type" : "string"}""" |> shouldBeEqual String

[<Fact>]
let ``parse date-time``() =
    """{
        "type" : "string",
        "format" : "date-time"
    }"""
    |> shouldBeEqual DateTime

[<Fact>]
let ``parse date``() =
    """{
        "type" : "string",
        "format" : "date"
    }"""
    |> shouldBeEqual Date

[<Fact>]
let ``parse enum``() =
    """{
        "type" : "string",
        "description" : "pet status in the store",
        "enum" : ["available", "pending", "sold"]
    }"""
    |> shouldBeEqual(Enum([| "available"; "pending"; "sold" |], "string"))

[<Fact>]
let ``parse definition reference``() =
    """{"$ref" : "#/definitions/Tag"}"""
    |> shouldBeEqual(Reference "#/definitions/Tag")

[<Fact>]
let ``parse array of definitions``() =
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
    |> shouldBeEqual(Array(Reference "#/definitions/Tag"))

[<Fact>]
let ``parse array of string``() =
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
    |> shouldBeEqual(Array String)

[<Fact>]
let ``parse map of definitions``() =
    """{
        "type": "object",
        "additionalProperties": {
            "$ref": "#/definitions/Tag"
        }
    }"""
    |> shouldBeEqual(Dictionary(Reference "#/definitions/Tag"))

[<Fact>]
let ``parse map of string``() =
    """{
        "type": "object",
        "additionalProperties": {
            "type": "string"
        }
    }"""
    |> shouldBeEqual(Dictionary String)
