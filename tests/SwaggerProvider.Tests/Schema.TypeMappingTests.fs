module SwaggerProvider.Tests.Schema_TypeMappingTests

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
let ``required string date format maps to DateTimeOffset when useDateOnly is false``() =
    let ty = compilePropertyType "          type: string\n          format: date\n" true

    ty |> shouldEqual typeof<DateTimeOffset>

[<Fact>]
let ``required string date format maps to DateOnly when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithDateOnly "          type: string\n          format: date\n" true

    ty |> shouldEqual typeof<DateOnly>

[<Fact>]
let ``required string time format falls back to string when useDateOnly is false``() =
    // The test helper compiles with useDateOnly=false, so TimeOnly is not used
    let ty = compilePropertyType "          type: string\n          format: time\n" true
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``required string time format maps to TimeOnly when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithDateOnly "          type: string\n          format: time\n" true

    ty |> shouldEqual typeof<TimeOnly>

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
let ``optional DateOnly maps to Option<DateOnly> when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithDateOnly "          type: string\n          format: date\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<DateOnly>))

[<Fact>]
let ``optional TimeOnly maps to Option<TimeOnly> when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithDateOnly "          type: string\n          format: time\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<TimeOnly>))

[<Fact>]
let ``optional Guid maps to Option<Guid>``() =
    let ty =
        compilePropertyType "          type: string\n          format: uuid\n" false

    ty |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<Guid>))

// ── Optional reference types are wrapped in Option<T> ────────────────────────

[<Fact>]
let ``optional string maps to Option<string>``() =
    let ty = compilePropertyType "          type: string\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<string>))

[<Fact>]
let ``optional byte array maps to Option<byte[]>``() =
    let ty =
        compilePropertyType "          type: string\n          format: byte\n" false

    // byte[*] is a reference type — wrapped in Option<T> when non-required
    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<byte>.MakeArrayType(1)))

[<Fact>]
let ``optional binary maps to Option<Stream>``() =
    let ty =
        compilePropertyType "          type: string\n          format: binary\n" false

    ty
    |> shouldEqual(typedefof<Option<_>>.MakeGenericType(typeof<IO.Stream>))

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
let ``PreferNullable: optional string stays as plain string``() =
    // With provideNullable=true, reference types are left as plain CLR-nullable types
    // (Nullable<T> is not valid for reference types).
    let ty = compilePropertyTypeWith true "          type: string\n" false
    ty |> shouldEqual typeof<string>

[<Fact>]
let ``PreferNullable: optional binary stays as plain byte array``() =
    let ty =
        compilePropertyTypeWith true "          type: string\n          format: byte\n" false

    ty |> shouldEqual(typeof<byte>.MakeArrayType(1))

[<Fact>]
let ``PreferNullable: optional binary (base64) stays as plain Stream``() =
    let ty =
        compilePropertyTypeWith true "          type: string\n          format: binary\n" false

    ty |> shouldEqual typeof<IO.Stream>

// ── PreferNullable + useDateOnly: value-type date/time formats use Nullable<T> ──────────────
// When both provideNullable=true and useDateOnly=true, optional DateOnly/TimeOnly properties
// should be wrapped in Nullable<T> (not Option<T>), consistent with other value types.

[<Fact>]
let ``PreferNullable: optional DateOnly maps to Nullable<DateOnly> when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithNullableAndDateOnly "          type: string\n          format: date\n" false

    ty |> shouldEqual typeof<System.Nullable<DateOnly>>

[<Fact>]
let ``PreferNullable: required DateOnly is not wrapped when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithNullableAndDateOnly "          type: string\n          format: date\n" true

    ty |> shouldEqual typeof<DateOnly>

[<Fact>]
let ``PreferNullable: optional TimeOnly maps to Nullable<TimeOnly> when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithNullableAndDateOnly "          type: string\n          format: time\n" false

    ty |> shouldEqual typeof<System.Nullable<TimeOnly>>

[<Fact>]
let ``PreferNullable: required TimeOnly is not wrapped when useDateOnly is true``() =
    let ty =
        compilePropertyTypeWithNullableAndDateOnly "          type: string\n          format: time\n" true

    ty |> shouldEqual typeof<TimeOnly>

// ── Named enum schema generation ──────────────────────────────────────────────────────────────
// When a top-level component schema has `type: string` (or `integer`) plus an `enum` list,
// DefinitionCompiler should emit a CLI enum type instead of a plain string/int.

let private enumTestSchema(schemaBody: string) =
    sprintf
        """openapi: "3.0.0"
info:
  title: EnumTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    Status:
%s"""
        schemaBody

[<Fact>]
let ``named string enum schema compiles to a CLI enum type``() =
    let schema =
        enumTestSchema
            """      type: string
      enum:
        - active
        - inactive
        - pending"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")
    statusTy.IsEnum |> shouldEqual true

[<Fact>]
let ``named string enum has correct member names``() =
    let schema =
        enumTestSchema
            """      type: string
      enum:
        - active
        - inactive
        - pending"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")
    let memberNames = statusTy.GetFields() |> Array.map(fun f -> f.Name) |> Array.sort

    memberNames |> shouldEqual [| "Active"; "Inactive"; "Pending" |]

[<Fact>]
let ``named string enum member values are sequential integers``() =
    let schema =
        enumTestSchema
            """      type: string
      enum:
        - active
        - inactive
        - pending"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")

    let values =
        statusTy.GetFields()
        |> Array.filter(fun f -> f.IsLiteral)
        |> Array.sortBy(fun f -> f.Name)
        |> Array.map(fun f -> f.GetRawConstantValue() :?> int32)

    values |> shouldEqual [| 0; 1; 2 |]

[<Fact>]
let ``named string enum has JsonStringEnumConverter attribute``() =
    let schema =
        enumTestSchema
            """      type: string
      enum:
        - active
        - inactive"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")

    statusTy.GetCustomAttributesData()
    |> Seq.exists(fun a -> a.Constructor.DeclaringType = typeof<System.Text.Json.Serialization.JsonConverterAttribute>)
    |> shouldEqual true

[<Fact>]
let ``named string enum members have JsonStringEnumMemberName attributes for wire values``() =
    let schema =
        enumTestSchema
            """      type: string
      enum:
        - active
        - in-active"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")

    let wireNames =
        statusTy.GetFields()
        |> Array.filter(fun f -> f.IsLiteral)
        |> Array.collect(fun f ->
            f.GetCustomAttributesData()
            |> Seq.filter(fun a -> a.Constructor.DeclaringType = typeof<System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute>)
            |> Seq.map(fun a -> a.ConstructorArguments.[0].Value :?> string)
            |> Seq.toArray)
        |> Array.sort

    wireNames |> shouldEqual [| "active"; "in-active" |]

[<Fact>]
let ``named integer enum schema compiles to a CLI enum type``() =
    let schema =
        enumTestSchema
            """      type: integer
      enum:
        - 1
        - 2
        - 3"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")
    statusTy.IsEnum |> shouldEqual true

[<Fact>]
let ``named integer enum has correct integer values``() =
    let schema =
        enumTestSchema
            """      type: integer
      enum:
        - 10
        - 20
        - 30"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")

    let values =
        statusTy.GetFields()
        |> Array.filter(fun f -> f.IsLiteral)
        |> Array.map(fun f -> f.GetRawConstantValue() :?> int32)
        |> Array.sort

    values |> shouldEqual [| 10; 20; 30 |]

[<Fact>]
let ``named integer enum with int64 format has int64 underlying type``() =
    let schema =
        enumTestSchema
            """      type: integer
      format: int64
      enum:
        - 1000000000000
        - 2000000000000"""

    let types = compileV3Schema schema false
    let statusTy = types |> List.find(fun t -> t.Name = "Status")
    statusTy.IsEnum |> shouldEqual true
    Enum.GetUnderlyingType statusTy |> shouldEqual typeof<int64>

    let values =
        statusTy.GetFields()
        |> Array.filter(fun f -> f.IsLiteral)
        |> Array.map(fun f -> f.GetRawConstantValue() :?> int64)
        |> Array.sort

    values |> shouldEqual [| 1000000000000L; 2000000000000L |]

[<Fact>]
let ``optional named string enum property maps to Option<EnumType>``() =
    let schema =
        """openapi: "3.0.0"
info:
  title: EnumTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    Status:
      type: string
      enum:
        - active
        - inactive
    TestType:
      type: object
      properties:
        status:
          $ref: '#/components/schemas/Status'"""

    let types = compileV3Schema schema false
    let testType = types |> List.find(fun t -> t.Name = "TestType")
    let prop = testType.GetDeclaredProperty("Status")
    prop.PropertyType.IsGenericType |> shouldEqual true

    prop.PropertyType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<option<_>>

[<Fact>]
let ``required named string enum property maps to enum type directly``() =
    let schema =
        """openapi: "3.0.0"
info:
  title: EnumTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    Status:
      type: string
      enum:
        - active
        - inactive
    TestType:
      type: object
      required:
        - status
      properties:
        status:
          $ref: '#/components/schemas/Status'"""

    let types = compileV3Schema schema false
    let testType = types |> List.find(fun t -> t.Name = "TestType")
    let prop = testType.GetDeclaredProperty("Status")
    prop.PropertyType.IsEnum |> shouldEqual true
