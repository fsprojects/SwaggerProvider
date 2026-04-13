module Swagger.I0181.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/Schemas/issue181.yaml"

type MyApi = OpenApiClientProvider<Schema>

let inst = MyApi.Client()
