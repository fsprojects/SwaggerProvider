module Swagger.Instagram.Tests

open SwaggerProvider

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/Instagram.json"
type Instagram = SwaggerProvider<Schema>

let insta = Instagram.Client(Headers=[||], CustomizeHttpRequest=id)