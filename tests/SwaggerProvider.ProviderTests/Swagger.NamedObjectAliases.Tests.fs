module Swagger.NamedObjectAliases.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/Schemas/v3/named-object-alias-oneof.json"

type Api = OpenApiClientProvider<Schema, SsrfProtection=false>

[<Fact>]
let ``named component alias response resolves to the referenced object type``() =
    let methodInfo =
        typeof<Api.Client>.GetMethods()
        |> Array.filter(fun candidate -> candidate.Name = "GetParent")
        |> Array.exactlyOne

    let responseType = methodInfo.ReturnType.GetGenericArguments() |> Array.exactlyOne

    responseType |> shouldEqual typeof<Api.Parent_Child>

    let response = Api.Parent_Child("oneOf")
    response.ChildValue |> shouldEqual "oneOf"
