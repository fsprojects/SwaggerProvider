module SwaggerProvider.Tests.v3_Schema_ArrayAndMapTypeMappingTests

open System
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.v3.Compilers
open Xunit
open FsUnitTyped

/// Parse and compile a full OpenAPI v3 schema string, then return the .NET type of
/// the `Value` property on the `TestType` component schema.
let private compileSchemaAndGetValueType(schemaStr: string) : Type =
    let settings = OpenApiReaderSettings()
    settings.AddYamlReader()

    let readResult =
        Microsoft.OpenApi.OpenApiDocument.Parse(schemaStr, settings = settings)

    match readResult.Diagnostic with
    | null -> ()
    | diagnostic when diagnostic.Errors |> Seq.isEmpty |> not ->
        let errorText =
            diagnostic.Errors
            |> Seq.map string
            |> String.concat Environment.NewLine

        failwithf "Failed to parse OpenAPI schema:%s%s" Environment.NewLine errorText
    | _ -> ()

    let schema =
        match readResult.Document with
        | null -> failwith "Failed to parse OpenAPI schema: Document is null."
        | doc -> doc

    let defCompiler = DefinitionCompiler(schema, false, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)

    let types = defCompiler.Namespace.GetProvidedTypes()
    let testType = types |> List.find(fun t -> t.Name = "TestType")

    match testType.GetDeclaredProperty("Value") with
    | null -> failwith "Property 'Value' not found on TestType"
    | prop -> prop.PropertyType

/// Compile a minimal v3 schema where TestType.Value is defined by `propYaml`.
let private compilePropertyType (propYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: ArrayMapMappingTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    TestType:
      type: object
%s      properties:
        Value:
%s"""
            requiredBlock
            propYaml

    compileSchemaAndGetValueType schemaStr

// ── Required array types ──────────────────────────────────────────────────────

[<Fact>]
let ``required array of string maps to string array``() =
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: string\n" true

    ty |> shouldEqual(typeof<string>.MakeArrayType 1)

[<Fact>]
let ``required array of integer maps to int32 array``() =
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: integer\n" true

    ty |> shouldEqual(typeof<int32>.MakeArrayType 1)

[<Fact>]
let ``required array of boolean maps to bool array``() =
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: boolean\n" true

    ty |> shouldEqual(typeof<bool>.MakeArrayType 1)

[<Fact>]
let ``required array of number (double) maps to double array``() =
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: number\n            format: double\n" true

    ty |> shouldEqual(typeof<double>.MakeArrayType 1)

// ── Optional array types are NOT wrapped in Option (arrays are reference types) ─

[<Fact>]
let ``optional array of string is not wrapped in Option``() =
    // string[] is a reference type — not wrapped in Option<T> even when non-required
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: string\n" false

    ty |> shouldEqual(typeof<string>.MakeArrayType 1)

[<Fact>]
let ``optional array of integer is not wrapped in Option``() =
    // int32[] is a reference type — not wrapped in Option<T>
    let ty =
        compilePropertyType "          type: array\n          items:\n            type: integer\n" false

    ty |> shouldEqual(typeof<int32>.MakeArrayType 1)

// ── additionalProperties maps to Map<string, T> ──────────────────────────────

let private compileAdditionalPropertiesType(valuePropYaml: string) : Type =
    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: MapMappingTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    TestType:
      type: object
      properties:
        Value:
          type: object
          additionalProperties:
%s"""
            valuePropYaml

    compileSchemaAndGetValueType schemaStr

[<Fact>]
let ``additionalProperties string maps to Map<string, string>``() =
    let ty = compileAdditionalPropertiesType "            type: string\n"

    ty
    |> shouldEqual(typedefof<Map<string, obj>>.MakeGenericType(typeof<string>, typeof<string>))

[<Fact>]
let ``additionalProperties integer maps to Map<string, int32>``() =
    let ty = compileAdditionalPropertiesType "            type: integer\n"

    ty
    |> shouldEqual(typedefof<Map<string, obj>>.MakeGenericType(typeof<string>, typeof<int32>))

[<Fact>]
let ``additionalProperties boolean maps to Map<string, bool>``() =
    let ty = compileAdditionalPropertiesType "            type: boolean\n"

    ty
    |> shouldEqual(typedefof<Map<string, obj>>.MakeGenericType(typeof<string>, typeof<bool>))

// ── Array of $ref objects ─────────────────────────────────────────────────────

[<Fact>]
let ``required array of ref object maps to ProvidedType array``() =
    let schemaStr =
        """openapi: "3.0.0"
info:
  title: ArrayOfRefTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    Item:
      type: object
      properties:
        Id:
          type: integer
    TestType:
      type: object
      properties:
        Value:
          type: array
          items:
            $ref: '#/components/schemas/Item'
"""

    let ty = compileSchemaAndGetValueType schemaStr
    // Should be an array type (Item[])
    ty.IsArray |> shouldEqual true
    ty.GetElementType().Name |> shouldEqual "Item"
