module SwaggerProvider.DefinitionsTests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open FSharp.Data
open NUnit.Framework
open FsUnitTyped

let shouldBeEqual (expected:SchemaObject) content =
    content
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Parser.emptyDict
    |> shouldEqual expected

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
    |> shouldBeEqual (Reference "#/definitions/Tag")

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
    |> shouldBeEqual (Array (Reference "#/definitions/Tag"))

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
