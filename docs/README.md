
[![NuGet Version](https://badgen.net/nuget/v/SwaggerProvider)](https://www.nuget.org/packages/SwaggerProvider)
[![NuGet Downloads](https://badgen.net/nuget/dt/SwaggerProvider)](https://www.nuget.org/packages/SwaggerProvider)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](http://unlicense.org/)

**SwaggerProvider** is an F# generative [Type Provider](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) that auto-generates strongly-typed HTTP clients from [OpenAPI 3.0](https://swagger.io/specification/) and [Swagger 2.0](https://swagger.io/specification/v2/) schemas — no code generation step required.

The single provider, [OpenApiClientProvider](/OpenApiClientProvider), uses [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers/) to parse both OpenAPI and Swagger schemas in `JSON` and `YAML` formats, and targets `net8.0` and `net10.0`.

## Features

- **Zero code generation** — types are created at compile time from live or local schema files
- Supports **OpenAPI 3.0** and **Swagger 2.0** schemas in JSON and YAML formats
- Works in **F# scripts**, **.NET projects**, and **F# Interactive**
- Generates typed models, request/response types, and a typed HTTP client
- IDE auto-complete and type-checking for all API endpoints
- **SSRF protection** enabled by default (disable with `SsrfProtection=false` for local dev)
- **CancellationToken support** — every generated method accepts an optional `cancellationToken`

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

## Key Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Schema` | *(required)* | URL or file path to the OpenAPI/Swagger schema |
| `SsrfProtection` | `true` | Block HTTP and private IPs to prevent SSRF attacks |
| `PreferNullable` | `false` | Use `Nullable<_>` instead of `Option<_>` for optional fields |
| `PreferAsync` | `false` | Generate `Async<'T>` instead of `Task<'T>` |
| `IgnoreControllerPrefix` | `true` | Generate a single client class for all operations |
| `IgnoreOperationId` | `false` | Generate method names from paths instead of operation IDs |
| `IgnoreParseErrors` | `false` | Continue generation even when the parser reports schema warnings |

See [OpenApiClientProvider](/OpenApiClientProvider) for full parameter documentation and [Customization](/Customization) for advanced scenarios.
