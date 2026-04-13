module Swagger.I0279.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/Schemas/issue279.json"

type Immich = OpenApiClientProvider<Schema>

let inst = Immich.Client()
