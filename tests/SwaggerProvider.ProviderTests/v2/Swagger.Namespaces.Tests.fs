module Swagger.Namespaces.Tests

open SwaggerProvider

// https://github.com/Microsoft/OpenAPI.NET/issues/253

[<Literal>]
let SchemaGI = __SOURCE_DIRECTORY__ + "/../Schemas/v2/gettyimages.com.json"

type GI = SwaggerClientProvider<SchemaGI, IgnoreControllerPrefix=false>

let c1 = GI.ArtistsClient()
let c2 = GI.CustomersClient()

let x = GI.GettyImages.Models.Customers()
let y = GI.GettyImages.Models()

[<Literal>]
let SchemaCM = __SOURCE_DIRECTORY__ + "/../Schemas/v2/clickmeter.com.json"

type CM = SwaggerClientProvider<SchemaCM>
let cm = CM.Client()

let a = CM.Api.Core.Dto.ClickStream()
let b = CM.ClickMeter.Infrastructure.Validation.ValidationFailure()
