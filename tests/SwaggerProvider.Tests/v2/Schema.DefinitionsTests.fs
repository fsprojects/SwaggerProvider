module SwaggerProvider.Tests.v2.Schema_DefinitionsTests

open SwaggerProvider.Internal.v2.Parser.Schema
open SwaggerProvider.Internal.v2.Parser
open Expecto

let shouldBeEqual (expected: SchemaObject) content =
    content
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> fun actual -> Expect.equal actual expected "type definition parse"

[<Tests>]
let definitionsTests =
    testList "All/v2/DefinitionsTests" [

        testCase "parse boolean"
        <| fun _ ->
            """{
            "type" : "boolean"
        }"""
            |> shouldBeEqual Boolean

        testCase "parse int32"
        <| fun _ ->
            """{
            "type" : "integer",
            "format" : "int32",
            "description" : "User Status"
        }"""
            |> shouldBeEqual Int32

        testCase "parse int64"
        <| fun _ ->
            """{
            "type" : "integer",
            "format" : "int64"
        }"""
            |> shouldBeEqual Int64

        testCase "parse float"
        <| fun _ ->
            """{
            "type" : "number",
            "format" : "float"
        }"""
            |> shouldBeEqual Float

        testCase "parse double"
        <| fun _ ->
            """{
            "type" : "number",
            "format" : "double"
        }"""
            |> shouldBeEqual Double

        testCase "parse string"
        <| fun _ -> """{"type" : "string"}""" |> shouldBeEqual String

        testCase "parse date-time"
        <| fun _ ->
            """{
            "type" : "string",
            "format" : "date-time"
        }"""
            |> shouldBeEqual DateTime

        testCase "parse date"
        <| fun _ ->
            """{
            "type" : "string",
            "format" : "date"
        }"""
            |> shouldBeEqual Date

        testCase "parse enum"
        <| fun _ ->
            """{
            "type" : "string",
            "description" : "pet status in the store",
            "enum" : ["available", "pending", "sold"]
        }"""
            |> shouldBeEqual(Enum [| "available"; "pending"; "sold" |])

        testCase "parse definition reference"
        <| fun _ ->
            """{"$ref" : "#/definitions/Tag"}"""
            |> shouldBeEqual(Reference "#/definitions/Tag")

        testCase "parse array of definitions"
        <| fun _ ->
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

        testCase "parse array of string"
        <| fun _ ->
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

        testCase "parse map of definitions"
        <| fun _ ->
            """{
            "type": "object",
            "additionalProperties": {
                "$ref": "#/definitions/Tag"
            }
        }"""
            |> shouldBeEqual(Dictionary(Reference "#/definitions/Tag"))

        testCase "parse map of string"
        <| fun _ ->
            """{
            "type": "object",
            "additionalProperties": {
                "type": "string"
            }
        }"""
            |> shouldBeEqual(Dictionary String)
    ]
