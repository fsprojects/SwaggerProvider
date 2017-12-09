module Swagger.Namespaces.Tests

open SwaggerProvider

type GI = SwaggerProvider<"https://api.apis.guru/v2/specs/gettyimages.com/3/swagger.json", IgnoreControllerPrefix = false>

let c1 = GI.ArtistsClient()
let c2 = GI.UsageClient()

let x = GI.GettyImages.Models.Customers()
let y = GI.GettyImages.Models()

type CM = SwaggerProvider<"https://api.apis.guru/v2/specs/clickmeter.com/v2/swagger.json">
let cm = CM.Client()

let a = CM.Api.Core.Dto.ClickStream()
let b = CM.ClickMeter.Infrastructure.Validation.ValidationFailure()