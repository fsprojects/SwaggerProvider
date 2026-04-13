module SwaggerProvider.Tests.v3_Schema_TypeMappingTests

open System
open Xunit
open FsUnitTyped

// ── Required primitive types ─────────────────────────────────────────────────

[<Fact>]
let ``required boolean maps to bool``() =
    let ty = compilePropertyType "          type: boolean\n" true

    ty |> shouldEqual typeof<bool>

[<Fact>]
let ``required integer (no format) maps to int32``() =
    let ty = compilePropertyType "          type: integer\n" true

    ty |> shouldEqual typeof<int32>

[<Fact>]
let ``required integer int64 format maps to int64``() =
    let ty =
        compilePropertyType "          type: integer\n          format: int64\n" true

    ty |> shouldEqual typeof<int64>

[<Fact>]
let ``required number (no format) maps to float32``() =
    let ty = compilePropertyType "          type: number\n" true

    ty |> shouldEqual typeof<float32>

[<Fact>]
let ``required number double format maps to double``() =
    let ty =
        compilePropertyType "          type: number\n          format: double\n" true

    ty |> shouldEqual typeof<double>

// ── Required string formats ───────────────────────────────────────────────────

[<Fact>]
let ``required string (no format) maps to string``() =
    let ty = compilePropertyType "          type: string\n" true

    ty |> shouldEqual typeof<string>

[<Fact>]
let ``required string date-time format maps to DateTimeOffset``() =
    let ty =
        compilePropertyType "          type: string\n          format: date-time\n" true

    ty |> shouldEqual typeof<DateTimeOffset>

[<Fact>]
let ``required string date format maps to DateTimeOffset``() =
    let ty = compilePropertyType "          type: string\n          format: date\n" true

    ty |> shouldEqual typeof<DateTimeOffset>

[<Fact>]
let ``required string uuid format maps to Guid``() =
    let ty = compilePropertyType "          type: string\n          format: uuid\n" true

    ty |> shouldEqual typeof<Guid>

[<Fact>]
let ``required string byte format maps to byte array``() =
    let ty = compilePropertyType "          type: string\n          format: byte\n" true

    // DefinitionCompiler creates a rank-1 explicit array via MakeArrayType(1)
    ty |> shouldEqual(typeof<byte>.MakeArrayType(1))

[<Fact>]
let ``required string binary format maps to Stream``() =
    let ty =
        compilePropertyType "          type: string\n          format: binary\n" true

    ty |> shouldEqual typeof<IO.Stream>

// ── Optional (non-required) value types are wrapped in Option<T> ─────────────

[<Fact>]
let ``optional boolean maps to Option<bool>``() =
    let ty = compilePropertyType "          type: boolean\n" false

    ty |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<bool>))

[<Fact>]
let ``optional integer maps to Option<int32>``() =
    let ty = compilePropertyType "          type: integer\n" false

    ty |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<int32>))

[<Fact>]
let ``optional integer int64 maps to Option<int64>``() =
    let ty =
        compilePropertyType "          type: integer\n          format: int64\n" false

    ty |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<int64>))

[<Fact>]
let ``optional number maps to Option<float32>``() =
    let ty = compilePropertyType "          type: number\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<float32>))

[<Fact>]
let ``optional number double maps to Option<double>``() =
    let ty =
        compilePropertyType "          type: number\n          format: double\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<double>))

[<Fact>]
let ``optional DateTimeOffset maps to Option<DateTimeOffset>``() =
    let ty =
        compilePropertyType "          type: string\n          format: date-time\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<DateTimeOffset>))

[<Fact>]
let ``optional Guid maps to Option<Guid>``() =
    let ty =
        compilePropertyType "          type: string\n          format: uuid\n" false

    ty |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<Guid>))

// ── Optional reference types are NOT wrapped (they are already nullable) ─────

[<Fact>]
let ``optional string is not wrapped in Option``() =
    let ty = compilePropertyType "          type: string\n" false

    // string is a reference type — not wrapped in Option<T> even when non-required
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``optional byte array is not wrapped in Option``() =
    let ty =
        compilePropertyType "          type: string\n          format: byte\n" false

    // byte[*] is a reference type — not wrapped in Option<T>
    ty |> shouldEqual(typeof<byte>.MakeArrayType(1))

// ── $ref primitive-type alias helpers ────────────────────────────────────────

/// Compile a schema where `TestType.Value` directly references a component alias schema
/// (e.g., `$ref: '#/components/schemas/AliasType'`) and return the resolved .NET type.
let private compileDirectRefType (aliasYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: RefAliasTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    AliasType:
%s    TestType:
      type: object
%s      properties:
        Value:
          $ref: '#/components/schemas/AliasType'
"""
            (aliasYaml.TrimEnd() + "\n")
            requiredBlock

    compileSchemaAndGetValueType schemaStr

/// Compile a schema where `TestType.Value` uses `allOf: [$ref]` to reference a component alias
/// (the standard OpenAPI 3.0 pattern for annotating a reference) and return the resolved .NET type.
let private compileAllOfRefType (aliasYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: AllOfRefAliasTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    AliasType:
%s    TestType:
      type: object
%s      properties:
        Value:
          allOf:
            - $ref: '#/components/schemas/AliasType'
"""
            (aliasYaml.TrimEnd() + "\n")
            requiredBlock

    compileSchemaAndGetValueType schemaStr

// ── $ref to primitive-type alias (direct $ref) ───────────────────────────────

[<Fact>]
let ``direct $ref to string alias resolves to string``() =
    let ty = compileDirectRefType "      type: string\n" true
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``direct $ref to integer alias resolves to int32``() =
    let ty = compileDirectRefType "      type: integer\n" true
    ty |> shouldEqual typeof<int32>

[<Fact>]
let ``direct $ref to int64 alias resolves to int64``() =
    let ty = compileDirectRefType "      type: integer\n      format: int64\n" true
    ty |> shouldEqual typeof<int64>

[<Fact>]
let ``direct $ref to number alias resolves to float32``() =
    let ty = compileDirectRefType "      type: number\n" true
    ty |> shouldEqual typeof<float32>

[<Fact>]
let ``direct $ref to boolean alias resolves to bool``() =
    let ty = compileDirectRefType "      type: boolean\n" true
    ty |> shouldEqual typeof<bool>

[<Fact>]
let ``direct $ref to uuid string alias resolves to Guid``() =
    let ty = compileDirectRefType "      type: string\n      format: uuid\n" true
    ty |> shouldEqual typeof<Guid>

// ── $ref to primitive-type alias (via allOf wrapper) ─────────────────────────
// allOf: [$ref] is the standard OpenAPI 3.0 pattern for annotating a $ref with
// additional constraints (e.g. description, nullable) without repeating the schema.

[<Fact>]
let ``allOf $ref to string alias resolves to string``() =
    let ty = compileAllOfRefType "      type: string\n" true
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``allOf $ref to integer alias resolves to int32``() =
    let ty = compileAllOfRefType "      type: integer\n" true
    ty |> shouldEqual typeof<int32>

// ── $ref to primitive-type alias (via oneOf wrapper) ─────────────────────────
// oneOf: [$ref] with a single entry is semantically equivalent to a direct $ref.
// Some code generators (e.g., NSwag, Kiota) emit this form.

/// Compile a schema where `TestType.Value` uses `oneOf: [$ref]` to reference a component alias.
let private compileOneOfRefType (aliasYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: OneOfRefAliasTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    AliasType:
%s    TestType:
      type: object
%s      properties:
        Value:
          oneOf:
            - $ref: '#/components/schemas/AliasType'
"""
            (aliasYaml.TrimEnd() + "\n")
            requiredBlock

    compileSchemaAndGetValueType schemaStr

/// Compile a schema where `TestType.Value` uses `anyOf: [$ref]` to reference a component alias.
let private compileAnyOfRefType (aliasYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: AnyOfRefAliasTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    AliasType:
%s    TestType:
      type: object
%s      properties:
        Value:
          anyOf:
            - $ref: '#/components/schemas/AliasType'
"""
            (aliasYaml.TrimEnd() + "\n")
            requiredBlock

    compileSchemaAndGetValueType schemaStr

[<Fact>]
let ``oneOf $ref to string alias resolves to string``() =
    let ty = compileOneOfRefType "      type: string\n" true
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``oneOf $ref to integer alias resolves to int32``() =
    let ty = compileOneOfRefType "      type: integer\n" true
    ty |> shouldEqual typeof<int32>

[<Fact>]
let ``anyOf $ref to string alias resolves to string``() =
    let ty = compileAnyOfRefType "      type: string\n" true
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``anyOf $ref to integer alias resolves to int32``() =
    let ty = compileAnyOfRefType "      type: integer\n" true
    ty |> shouldEqual typeof<int32>

[<Fact>]
let ``optional oneOf $ref to integer alias resolves to Option<int32>``() =
    let ty = compileOneOfRefType "      type: integer\n" false
    ty |> shouldEqual typeof<int32 option>

[<Fact>]
let ``optional anyOf $ref to integer alias resolves to Option<int32>``() =
    let ty = compileAnyOfRefType "      type: integer\n" false
    ty |> shouldEqual typeof<int32 option>

// ── Optional $ref to primitive-type alias ─────────────────────────────────────
// When a $ref/allOf alias property is non-required, value types must be wrapped
// in Option<T> consistent with the behaviour of ordinary optional primitive properties.

[<Fact>]
let ``optional direct $ref to integer alias resolves to Option<int32>``() =
    let ty = compileDirectRefType "      type: integer\n" false
    ty |> shouldEqual typeof<int32 option>

[<Fact>]
let ``optional direct $ref to int64 alias resolves to Option<int64>``() =
    let ty = compileDirectRefType "      type: integer\n      format: int64\n" false
    ty |> shouldEqual typeof<int64 option>

[<Fact>]
let ``optional allOf $ref to integer alias resolves to Option<int32>``() =
    let ty = compileAllOfRefType "      type: integer\n" false
    ty |> shouldEqual typeof<int32 option>

[<Fact>]
let ``optional allOf $ref to int64 alias resolves to Option<int64>``() =
    let ty = compileAllOfRefType "      type: integer\n      format: int64\n" false
    ty |> shouldEqual typeof<int64 option>

// ── PreferNullable=true: optional value types use Nullable<T> ─────────────────
// When provideNullable=true, the DefinitionCompiler wraps optional value types
// in Nullable<T> instead of Option<T>.

[<Fact>]
let ``PreferNullable: optional boolean maps to Nullable<bool>``() =
    let ty = compilePropertyTypeWith true "          type: boolean\n" false

    ty |> shouldEqual typeof<System.Nullable<bool>>

[<Fact>]
let ``PreferNullable: optional integer maps to Nullable<int32>``() =
    let ty = compilePropertyTypeWith true "          type: integer\n" false

    ty |> shouldEqual typeof<System.Nullable<int32>>

[<Fact>]
let ``PreferNullable: optional int64 maps to Nullable<int64>``() =
    let ty =
        compilePropertyTypeWith true "          type: integer\n          format: int64\n" false

    ty |> shouldEqual typeof<System.Nullable<int64>>

[<Fact>]
let ``PreferNullable: required integer is not wrapped (Nullable only for optional)``() =
    let ty = compilePropertyTypeWith true "          type: integer\n" true
    ty |> shouldEqual typeof<int32>

[<Fact>]
let ``PreferNullable: optional string is not wrapped (reference type)``() =
    // Reference types like string are not wrapped in Nullable<T> since they are
    // already nullable by nature — same behaviour as Option mode.
    let ty = compilePropertyTypeWith true "          type: string\n" false
    ty |> shouldEqual typeof<string>
