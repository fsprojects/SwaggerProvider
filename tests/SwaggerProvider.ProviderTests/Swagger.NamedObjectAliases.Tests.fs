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

[<Literal>]
let MultipleAliasesSchema =
    __SOURCE_DIRECTORY__ + "/Schemas/named-object-alias-multiple.json"

type OneOfApi = OpenApiClientProvider<OneOfSchema, SsrfProtection=false>
type AnyOfApi = OpenApiClientProvider<AnyOfSchema, SsrfProtection=false>
type AllOfApi = OpenApiClientProvider<AllOfSchema, SsrfProtection=false>
type NamespacedApi = OpenApiClientProvider<NamespacedSchema, SsrfProtection=false>
type MultipleAliasesApi = OpenApiClientProvider<MultipleAliasesSchema, SsrfProtection=false>

let private getResponseType (clientType: System.Type) operationName =
    let methodInfo =
        clientType.GetMethods()
        |> Array.filter(fun candidate -> candidate.Name = operationName)
        |> Array.exactlyOne

    methodInfo.ReturnType.GetGenericArguments() |> Array.exactlyOne

let private getGeneratedTypeNames(generatedType: System.Type) =
    generatedType.DeclaringType.GetNestedTypes()
    |> Array.map(fun nestedType -> nestedType.Name)

[<Fact>]
let ``oneOf named component alias response resolves to child object type``() =
    getResponseType typeof<OneOfApi.Client> "GetParent"
    |> shouldEqual typeof<OneOfApi.Parent_Child>

    let response = OneOfApi.Parent_Child("oneOf")
    response.ChildValue |> shouldEqual "oneOf"

    let generatedTypeNames = getGeneratedTypeNames typeof<OneOfApi.Parent_Child>
    generatedTypeNames |> shouldNotContain "Parent"

    generatedTypeNames
    |> Array.filter(fun typeName -> typeName = "Parent_Child")
    |> Array.length
    |> shouldEqual 1

[<Fact>]
let ``anyOf named component alias response resolves to child object type``() =
    getResponseType typeof<AnyOfApi.Client> "GetParent"
    |> shouldEqual typeof<AnyOfApi.Parent_Child>

    let response = AnyOfApi.Parent_Child("anyOf")
    response.ChildValue |> shouldEqual "anyOf"

[<Fact>]
let ``allOf named component alias response resolves to child object type``() =
    getResponseType typeof<AllOfApi.Client> "GetParent"
    |> shouldEqual typeof<AllOfApi.Parent_Child>

    let response = AllOfApi.Parent_Child("allOf")
    response.ChildValue |> shouldEqual "allOf"

[<Fact>]
let ``namespaced aliases with equal leaf names register the child object only once``() =
    getResponseType typeof<NamespacedApi.Client> "GetParent"
    |> shouldEqual typeof<NamespacedApi.B.Parent>

    let response = NamespacedApi.B.Parent("namespaced")
    response.Value |> shouldEqual "namespaced"

[<Fact>]
let ``multiple aliases and alias chains resolve to their object types``() =
    getResponseType typeof<MultipleAliasesApi.Client> "GetParentA"
    |> shouldEqual typeof<MultipleAliasesApi.Child>

    getResponseType typeof<MultipleAliasesApi.Client> "GetParentB"
    |> shouldEqual typeof<MultipleAliasesApi.Child>

    getResponseType typeof<MultipleAliasesApi.Client> "GetAliasChain"
    |> shouldEqual typeof<MultipleAliasesApi.AliasC>

    let child = MultipleAliasesApi.Child("shared")
    child.Value |> shouldEqual "shared"

    let chained = MultipleAliasesApi.AliasC("chained")
    chained.ChainValue |> shouldEqual "chained"

    let generatedTypeNames = getGeneratedTypeNames typeof<MultipleAliasesApi.Child>
    generatedTypeNames |> shouldNotContain "ParentA"
    generatedTypeNames |> shouldNotContain "ParentB"
    generatedTypeNames |> shouldNotContain "AliasA"
    generatedTypeNames |> shouldNotContain "AliasB"
