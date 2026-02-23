module Swagger.I0219.Tests

open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/issue219.yaml"

type AcmeApi = OpenApiClientProvider<Schema>

let inst = AcmeApi.Client()

let askDetailRequest = new AcmeApi.AskDetailRequest(11)

let typedTask = inst.AskDetail(askDetailRequest: AcmeApi.AskDetailRequest)

typedTask.GetType().FullName.Contains("AskDetailResponse")
