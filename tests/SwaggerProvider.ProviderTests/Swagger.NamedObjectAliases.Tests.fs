module Swagger.NamedObjectAliases.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped

[<Literal>]
let OneOfSchema = __SOURCE_DIRECTORY__ + "/Schemas/named-object-alias-oneof.json"

[<Literal>]
let AnyOfSchema = __SOURCE_DIRECTORY__ + "/Schemas/named-object-alias-anyof.json"

[<Literal>]
let AllOfSchema = __SOURCE_DIRECTORY__ + "/Schemas/named-object-alias-allof.json"

[<Literal>]
let NamespacedSchema =
    __SOURCE_DIRECTORY__ + "/Schemas/named-object-alias-namespaced.json"

type OneOfApi = OpenApiClientProvider<OneOfSchema, SsrfProtection=false>
type AnyOfApi = OpenApiClientProvider<AnyOfSchema, SsrfProtection=false>
type AllOfApi = OpenApiClientProvider<AllOfSchema, SsrfProtection=false>
type NamespacedApi = OpenApiClientProvider<NamespacedSchema, SsrfProtection=false>

let private oneOfClient = OneOfApi.Client()
let private anyOfClient = AnyOfApi.Client()
let private allOfClient = AllOfApi.Client()
let private namespacedClient = NamespacedApi.Client()

// These helpers are compile-time assertions that operation responses expose the child property.
let private getOneOfChildValue() =
    task {
        let! response = oneOfClient.GetParent()
        return response.ChildValue
    }

let private getAnyOfChildValue() =
    task {
        let! response = anyOfClient.GetParent()
        return response.ChildValue
    }

let private getAllOfChildValue() =
    task {
        let! response = allOfClient.GetParent()
        return response.ChildValue
    }

let private getNamespacedValue() =
    task {
        let! response = namespacedClient.GetParent()
        return response.Value
    }

let private getResponseType(client: obj) =
    let methodInfo =
        client.GetType().GetMethods()
        |> Array.filter(fun candidate -> candidate.Name = "GetParent")
        |> Array.exactlyOne

    methodInfo.ReturnType.GetGenericArguments() |> Array.exactlyOne

[<Fact>]
let ``oneOf named component alias response resolves to child object type``() =
    getResponseType oneOfClient
    |> shouldEqual typeof<OneOfApi.Parent_Child>

    let response = OneOfApi.Parent_Child("oneOf")
    response.ChildValue |> shouldEqual "oneOf"

[<Fact>]
let ``anyOf named component alias response resolves to child object type``() =
    getResponseType anyOfClient
    |> shouldEqual typeof<AnyOfApi.Parent_Child>

    let response = AnyOfApi.Parent_Child("anyOf")
    response.ChildValue |> shouldEqual "anyOf"

[<Fact>]
let ``allOf named component alias response resolves to child object type``() =
    getResponseType allOfClient
    |> shouldEqual typeof<AllOfApi.Parent_Child>

    let response = AllOfApi.Parent_Child("allOf")
    response.ChildValue |> shouldEqual "allOf"

[<Fact>]
let ``namespaced aliases with equal leaf names register the child object only once``() =
    getResponseType namespacedClient
    |> shouldEqual typeof<NamespacedApi.B.Parent>

    let response = NamespacedApi.B.Parent("namespaced")
    response.Value |> shouldEqual "namespaced"
