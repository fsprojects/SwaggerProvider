module Swagger.SchemaReaderErrors.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped

[<Literal>]
let ValidSchema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/petstore.yaml"

[<Literal>]
let SchemaWithErrors =
    __SOURCE_DIRECTORY__
    + "/../Schemas/v3/nullable-parameter-issue261.json"

type ValidApi = OpenApiClientProvider<ValidSchema>

type ApiWithErrors = OpenApiClientProvider<SchemaWithErrors, IgnoreParseErrors=true>

[<Fact>]
let ``SchemaReaderErrors is empty for a valid schema``() =
    ValidApi.SchemaReaderErrors |> shouldEqual []

[<Fact>]
let ``SchemaReaderErrors is non-empty when IgnoreParseErrors=true and schema has validation errors``() =
    ApiWithErrors.SchemaReaderErrors |> List.isEmpty |> shouldEqual false

[<Fact>]
let ``SchemaReaderErrors entries contain message and pointer``() =
    let errors = ApiWithErrors.SchemaReaderErrors
    errors |> List.isEmpty |> shouldEqual false

    for entry in errors do
        entry.Contains(" @ ") |> shouldEqual true
