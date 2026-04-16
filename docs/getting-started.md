# SwaggerProvider

**SwaggerProvider** is an F# generative [Type Provider](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) that auto-generates strongly-typed HTTP clients from [OpenAPI 3.0](https://swagger.io/specification/) and [Swagger 2.0](https://swagger.io/specification/v2/) schemas — no code generation step required.

The single provider, [OpenApiClientProvider](/OpenApiClientProvider), uses [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers/) to parse both OpenAPI and Swagger schemas in `JSON` and `YAML` formats, and targets `net8.0` and `net10.0`.

## Getting Started

### F# Interactive

Create a new F# script file (e.g. `openapi.fsx`) and paste:

```fsharp
#r "nuget: SwaggerProvider"

open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
type PetStore = OpenApiClientProvider<Schema>

let client = PetStore.Client()

client.GetInventory()
|> Async.AwaitTask
|> Async.RunSynchronously
```

### New Project

```bash
dotnet new console --name apiclient --language F#
cd apiclient
dotnet add package SwaggerProvider
```

Replace the content of `Program.fs` with:

```fsharp
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
type PetStore = OpenApiClientProvider<Schema>

[<EntryPoint>]
let main argv =
    let client = PetStore.Client()
    client.GetInventory()
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> printfn "%O"
    0
```

Then build and run:

```bash
dotnet run
```

See [OpenApiClientProvider](/OpenApiClientProvider) for full parameter documentation and [Customization](/Customization) for advanced scenarios.
