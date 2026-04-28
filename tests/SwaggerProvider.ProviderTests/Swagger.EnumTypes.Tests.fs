module Swagger.EnumTypes.Tests

open Xunit
open FsUnitTyped
open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/Schemas/enum-types.yaml"

type EnumApi = OpenApiClientProvider<Schema, SsrfProtection=false>

// Compile-time verification: member names are accessible.
// The sanitised member names must match what DefinitionCompiler produces.
let _: EnumApi.StringStatus = EnumApi.StringStatus.Active
let _: EnumApi.StringStatus = EnumApi.StringStatus.InActive
let _: EnumApi.StringStatus = EnumApi.StringStatus.Pending
let _: EnumApi.IntStatus = EnumApi.IntStatus.V200
let _: EnumApi.IntStatus = EnumApi.IntStatus.V404
let _: EnumApi.IntStatus = EnumApi.IntStatus.V500
let _: EnumApi.LargeCode = EnumApi.LargeCode.V1
let _: EnumApi.LargeCode = EnumApi.LargeCode.V2
let _: EnumApi.LargeCode = EnumApi.LargeCode.V3

// ── String enum ────────────────────────────────────────────────────────────

[<Fact>]
let ``string enum is a CLI enum type``() =
    typeof<EnumApi.StringStatus>.IsEnum |> shouldEqual true

[<Fact>]
let ``string enum has int32 underlying type``() =
    System.Enum.GetUnderlyingType typeof<EnumApi.StringStatus>
    |> shouldEqual typeof<int32>

[<Fact>]
let ``string enum member names are sanitised from OpenAPI values``() =
    System.Enum.GetNames typeof<EnumApi.StringStatus>
    |> Array.sort
    |> shouldEqual [| "Active"; "InActive"; "Pending" |]

// ── Integer (int32) enum ───────────────────────────────────────────────────

[<Fact>]
let ``int32 enum is a CLI enum type``() =
    typeof<EnumApi.IntStatus>.IsEnum |> shouldEqual true

[<Fact>]
let ``int32 enum has int32 underlying type``() =
    System.Enum.GetUnderlyingType typeof<EnumApi.IntStatus>
    |> shouldEqual typeof<int32>

[<Fact>]
let ``int32 enum has correct integer values``() =
    int EnumApi.IntStatus.V200 |> shouldEqual 200
    int EnumApi.IntStatus.V404 |> shouldEqual 404
    int EnumApi.IntStatus.V500 |> shouldEqual 500

// ── Integer (int64) enum ───────────────────────────────────────────────────

[<Fact>]
let ``int64 enum is a CLI enum type``() =
    typeof<EnumApi.LargeCode>.IsEnum |> shouldEqual true

[<Fact>]
let ``int64 enum has int64 underlying type``() =
    System.Enum.GetUnderlyingType typeof<EnumApi.LargeCode>
    |> shouldEqual typeof<int64>

[<Fact>]
let ``int64 enum has correct integer values``() =
    int64 EnumApi.LargeCode.V1 |> shouldEqual 1L
    int64 EnumApi.LargeCode.V2 |> shouldEqual 2L
    int64 EnumApi.LargeCode.V3 |> shouldEqual 3L
