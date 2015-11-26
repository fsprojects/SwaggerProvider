namespace SwaggerProvider

open System
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open FSharp.Configuration.Helper

/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    static do
      // When SwaggerProvider is installed via NuGet/Paket, the Newtonsoft.Json assembly and
      // will appear typically in "../../*/lib/net40". To support this, we look at
      // SwaggerProvider.dll.config which has this pattern in custom key "ProbingLocations".
      // Here, we resolve assemblies by looking into the specified search paths.
      AppDomain.CurrentDomain.add_AssemblyResolve(fun source args ->
        SwaggerProvider.Internal.Configuration.resolveReferencedAssembly args.Name)

    let context = new Context(this, cfg)
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        this.AddNamespace(
            SwaggerProviderConfig.NameSpace,
            [SwaggerProviderConfig.typedSwaggerProvider context])