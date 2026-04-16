# SwaggerProvider

[![NuGet Version](https://badgen.net/nuget/v/SwaggerProvider)](https://www.nuget.org/packages/SwaggerProvider)
[![NuGet Downloads](https://badgen.net/nuget/dt/SwaggerProvider)](https://www.nuget.org/packages/SwaggerProvider)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](http://unlicense.org/)

**SwaggerProvider** is an F# library of generative [Type Providers](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) that auto-generate strongly-typed HTTP client code from [OpenAPI 3.0](https://swagger.io/specification/) and [Swagger 2.0](https://swagger.io/specification/v2/) schemas — no code generation step required.

📚 **Full documentation:** <https://fsprojects.github.io/SwaggerProvider/>

## Quick Start

```fsharp
#r "nuget: SwaggerProvider"
open SwaggerProvider

let [<Literal>] Schema = "https://petstore.swagger.io/v2/swagger.json"
type PetStore = OpenApiClientProvider<Schema>

let client = PetStore.Client()
client.GetInventory() |> Async.AwaitTask |> Async.RunSynchronously
```

## Features

- **Compile-Time Types** — generated at compile time directly from live or local schema files, no separate codegen step needed
- Supports **OpenAPI 3.0** and **Swagger 2.0** schemas in JSON and YAML formats
- **Works Everywhere** — F# scripts, .NET projects, and F# Interactive with full IntelliSense and type-checking
- **SSRF Protection** — blocks HTTP and private IP addresses by default to prevent server-side request forgery attacks
- **CancellationToken Support** — every generated method accepts an optional CancellationToken for cancelling long-running requests
- **Fully Customizable** — bring your own HttpClient, DelegatingHandler, or override JSON serialization

## Installation

```bash
dotnet add package SwaggerProvider
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

See the [full documentation](https://fsprojects.github.io/SwaggerProvider/) for more details and examples.

## Maintainer(s)

- [@sergey-tihon](https://github.com/sergey-tihon)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) — F# Community Project Incubation Space.
