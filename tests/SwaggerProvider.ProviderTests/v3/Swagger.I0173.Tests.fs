module Swagger.I0173.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/issue173.json"

type OdhApiTourism = OpenApiClientProvider<Schema>

let inst = OdhApiTourism.Client()
