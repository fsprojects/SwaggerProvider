# OpenApi Client Provider

OpenApiClientProvider is generative F# Type Provider, build on top of [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers/) schema parser that supports 3.0 and 2.0 schema formats.

```fsharp
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
type PetStore = OpenApiClientProvider<Schema>
let client = PetStore.Client()
```

## Parameters

`OpenApiClientProvider` supports following configuration parametes

| Parameter | Description |
|-----------|-------------|
| `Schema` | Url or Path to Swagger schema file. |
| `IgnoreOperationId` | Do not use `operationsId` and generate method names using `path` only. Default value `false`. |
| `IgnoreControllerPrefix` | Do not parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`. |
| `PreferNullable` | Provide `Nullable<_>` for not required properties, instead of `Option<_>`. Defaults value `false`. |
| `PreferAsync` | Generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults value `false`. |

More configuration scenarios are described in [Customization section](/Customization)

## Sample

TODO: