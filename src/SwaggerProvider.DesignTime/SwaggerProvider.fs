namespace SwaggerProvider

open System
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open SwaggerProvider.Internal.Configuration

module Handlers =

  let logError event (ex: exn) =
    let ex =
      match ex with
      | :? TypeInitializationException as typInit -> typInit.InnerException
      | _ -> ex
    Logging.logf "[%s]\t%s: %s\n%s" event (ex.GetType().Name) ex.Message ex.StackTrace
  let logResolve kind (args: ResolveEventArgs) = Logging.logf "[%s]\t%s on behalf of %s" kind args.Name args.RequestingAssembly.FullName

/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerTypeProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg)

    static do
      // When SwaggerProvider is installed via NuGet/Paket, the Newtonsoft.Json assembly and
      // will appear typically in "../../*/lib/net40". To support this, we look at
      // SwaggerProvider.dll.config which has this pattern in custom key "ProbingLocations".
      // Here, we resolve assemblies by looking into the specified search paths.
      AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->
        Handlers.logResolve "AssemblyResolve" args
        resolveReferencedAssembly args.Name
      )
      AppDomain.CurrentDomain.add_TypeResolve(fun _ args ->
        Handlers.logResolve "TypeResolve" args
        resolveReferencedAssembly args.Name
      )
      AppDomain.CurrentDomain.FirstChanceException.Add(fun args -> Handlers.logError "FirstChanceException" args.Exception)
      AppDomain.CurrentDomain.UnhandledException.Add(fun args -> Handlers.logError "UnhandledException" (args.ExceptionObject :?> exn))

    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg

        this.AddNamespace(
            SwaggerProviderConfig.NameSpace,
            [SwaggerProviderConfig.typedSwaggerProvider this.TargetContext cfg.RuntimeAssembly])
