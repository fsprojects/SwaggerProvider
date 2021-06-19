module Swagger.I0181.Tests

open SwaggerProvider

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/issue181.yaml"
type MyApi  = OpenApiClientProvider<Schema>

let inst = MyApi.Client()
