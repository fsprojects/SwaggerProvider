module Swagger.Instagram.Tests

open SwaggerProvider

let [<Literal>] schema = __SOURCE_DIRECTORY__ + "/Schemas/Instagram.json"
type Instagram = SwaggerProvider<schema>

