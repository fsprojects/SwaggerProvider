# OpenAPI Client Provider

OpenApiClientProvider is a generative F# Type Provider, built on top of [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers/) schema parser that supports 3.0 and 2.0 schema formats.

```fsharp
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
type PetStore = OpenApiClientProvider<Schema>
let client = PetStore.Client()
```

## Parameters

`OpenApiClientProvider` supports the following configuration parameters

| Parameter | Description |
|-----------|-------------|
| `Schema` | Url or Path to Swagger schema file. |
| `IgnoreOperationId` | Do not use `operationsId` and generate method names using `path` only. Default value `false`. |
| `IgnoreControllerPrefix` | Do not parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`. |
| `PreferNullable` | Provide `Nullable<_>` for not required properties, instead of `Option<_>`. Defaults value `false`. |
| `PreferAsync` | Generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults value `false`. |
| `SsrfProtection` | Enable SSRF protection (blocks HTTP and localhost). Set to `false` for development/testing. Default value `true`. |

More configuration scenarios are described in [Customization section](/Customization)

## Security (SSRF Protection)

By default, SwaggerProvider blocks HTTP URLs and localhost/private IP addresses to prevent [SSRF attacks](https://owasp.org/www-community/attacks/Server_Side_Request_Forgery). 

For **development and testing** with local servers, disable SSRF protection:

```fsharp
// Development: Allow HTTP and localhost
type LocalApi = OpenApiClientProvider<"http://localhost:5000/swagger.json", SsrfProtection=false>

// Production: HTTPS with SSRF protection (default)
type ProdApi = OpenApiClientProvider<"https://api.example.com/swagger.json">
```

**Warning:** Never set `SsrfProtection=false` in production code.

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
    // `BaseAddress` uri should end with '/' because TP generate relative uri
    let baseUri = Uri("https://petstore.swagger.io/v2/")
    use httpClient = new HttpClient(handler, true, BaseAddress=baseUri)
    // You can provide your instance of `HttpClient` to the provided api client
    // or change it any time in runtime using `client.HttpClient` property
    let client = PetStore.Client(httpClient)

    task {
        // Create a new instance of the provided type and add to store
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
