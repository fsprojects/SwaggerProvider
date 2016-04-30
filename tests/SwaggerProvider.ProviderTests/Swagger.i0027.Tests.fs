module Swagger.I0027.Tests

open SwaggerProvider

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/i0027.json"
type PSwagger = SwaggerProvider<Schema>

let inst = PSwagger()
