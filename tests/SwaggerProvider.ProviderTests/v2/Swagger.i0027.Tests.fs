module Swagger.I0027.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/i0027.json"

type PSwagger = SwaggerClientProvider<Schema>

let inst = PSwagger.Client()
