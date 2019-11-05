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

Sample uses [TaskBuilder.fs](https://github.com/rspeele/TaskBuilder.fs) (F# computation expression builder for System.Threading.Tasks) that will become part of [Fsharp.Core.dll] one day [[WIP, RFC FS-1072] task support](https://github.com/dotnet/fsharp/pull/6811).

```fsharp
open System
open System.Net.Http
open FSharp.Control.Tasks.V2
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
// By default provided methods return Task<'a> 
// and uses Option<'a> for optional params
type PetStore = OpenApiClientProvider<Schema>

[<EntryPoint>]
let main argv =
    // `UseCookies = false` is required if you use Cookie Parameters
    let handler = new HttpClientHandler (UseCookies = false)
    // `BaseAddress` uri should ends with '/' because TP generate relative uri
    let baseUri = Uri("https://petstore.swagger.io/v2/")
    use httpClient = new HttpClient(handler, true, BaseAddress=baseUri)
    // You can provide your instance of `HttpClient` to provided api client
    // or change it any time in runtime using `client.HttpClient` property
    let client = PetStore.Client(httpClient)

    task {
        // Create new instance of provided type and add to store
        let pet = PetStore.Pet(Id = Some(24L), Name = "Shani")
        do! client.AddPet(pet)

        // Request data back and deserialize to provided type
        let! myPet = client.GetPetById(24L)
        printfn "Waw, my name is %A" myPet.Name
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously
    0
```