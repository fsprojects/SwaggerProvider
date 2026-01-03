module Swagger.I0279.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/issue279.json"

type Immich = OpenApiClientProvider<Schema>

let inst = Immich.Client()
