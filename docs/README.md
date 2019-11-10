
[![NuGet Badge](https://buildstats.info/nuget/SwaggerProvider?includePreReleases=true)](https://www.nuget.org/packages/SwaggerProvider)

`SwaggerProvider` is an umbrella project for two F# generative Type Providers that generate object model and HTTP clients for APIs described by [OpenApi 3.0](https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.2.md) and [Swagger 2.0](https://github.com/OAI/OpenAPI-Specification/blob/master/versions/2.0.md) schemas
- [OpenApiClientProvider](/OpenApiClientProvider) <Badge type="success">New</Badge> - uses [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers/) to parse schema. Support both OpenApi ans Swagger schemas, but Swagger support is limited.
- [SwaggerClientProvider](/SwaggerClientProvider) - uses custom old good Swagger 2.0 schema parser and tested on several hundreds schemas available in [APIs.guru](https://apis.guru/openapi-directory/) (Wikipedia for WEB APIs)

Type Providers support schemas in `JSON` & `YAML` formats and runs on `netcoreapp3.0` and `net46`.

### Getting started

Create new F# `netcoreapp3.0` project and add reference to latest [SwaggerProvider](https://www.nuget.org/packages/SwaggerProvider) NuGet package
 
```bash
dotnet new console --name apiclient --language F#
cd apiclient
dotnet add package SwaggerProvider --version 0.10.0-beta11
```

replace content of `Program.fs` file by

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

build and run project 

```bash
dotnet run 
```

in the console you should see printed inventory from the server.

### Intellisense 

Intellisense works in your favorite IDE.

| [Visual Studio Code](https://code.visualstudio.com) + [Ionide](http://ionide.io) | [Rider](https://www.jetbrains.com/help/rider/F_Sharp.html) |
|-----------|-------------|
| <ImageZoom src="files/OpenApiClientProvider_Ionide.png" /> | <ImageZoom src="files/OpenApiClientProvider_Rider.png" /> | 

| [Visual Studio for Mac](https://visualstudio.microsoft.com/vs/mac/) | [Visual Studio](https://visualstudio.microsoft.com/vs/) |
|-----------|-------------|
| <ImageZoom src="files/OpenApiClientProvider_VS4Mac.png" /> | <ImageZoom src="files/OpenApiClientProvider_VS.png" /> | 