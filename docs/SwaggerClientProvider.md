# Swagger Client Provider

<Note type="warning">

The SwaggerClientProvider is outdated. There are no plans to improve the custom Swagger 2.0 schema parser or bring new features to this type provider. We hope to remove it from the source code when users migrate to [OpenApiClientProvider](/OpenApiClientProvider) and OpenApi 3.0 schemas.

</Note>

SwaggerClientProvider is a generative F# Type Provider, built on top of a custom Swagger schema parser that supports **only** 2.0 schema format.

```fsharp
open SwaggerProvider

let [<Literal>]schema = "http://petstore.swagger.io/v2/swagger.json"
type PetStore = SwaggerClientProvider<schema>
let petStore = PetStore.Client()
```

## Parameters

When you use TP you can specify the following parameters

| Parameter | Description |
|-----------|-------------|
| `Schema` | Url or Path to Swagger schema file. |
| `Headers` | HTTP Headers required to access the schema. |
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
type LocalApi = SwaggerClientProvider<"http://localhost:5000/swagger.json", SsrfProtection=false>

// Production: HTTPS with SSRF protection (default)
type ProdApi = SwaggerClientProvider<"https://api.example.com/swagger.json">
```

**Warning:** Never set `SsrfProtection=false` in production code.

## Sample

The usage is very similar to [OpenApiClientProvider](/OpenApiClientProvider#sample)

```fsharp
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
// Explicitly request Async<'a> methods instead of Task<'a> 
type PetStore = SwaggerClientProvider<Schema, PreferAsync=true>

[<EntryPoint>]
let main argv =
    // Type Provider creates HttpClient for you under the hood
    let client = PetStore.Client()
    async {
        // Create a new instance of the provided type and add it to the store
        let pet = PetStore.Pet(Id = Some(24L), Name = "Shani")
        do! client.AddPet(pet)

        // Request data back and deserialize to provided type
        let! myPet = client.GetPetById(24L)
        printfn "Waw, my name is %A" myPet.Name
    }
    |> Async.RunSynchronously
    0
```
