namespace SwaggerProvider

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Swagger
open SwaggerProvider.Internal
open SwaggerProvider.Internal.v3.Compilers

module OpenApiCache =
    let providedTypes = Caching.createInMemoryCache(TimeSpan.FromSeconds 30.0)

/// The Open API Provider.
[<TypeProvider>]
type public OpenApiClientTypeProvider(cfg: TypeProviderConfig) as this =
    inherit
        TypeProviderForNamespaces(
            cfg,
            assemblyReplacementMap = [ ("SwaggerProvider.DesignTime", "SwaggerProvider.Runtime") ],
            addDefaultProbingLocation = true
        )

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<ProvidedApiClientBase>.Assembly.GetName().Name = asm.GetName().Name)

    let myParamType =
        let t =
            ProvidedTypeDefinition(asm, ns, "OpenApiClientProvider", Some typeof<obj>, isErased = false)

        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)
              ProvidedStaticParameter("IgnoreControllerPrefix", typeof<bool>, true)
              ProvidedStaticParameter("PreferNullable", typeof<bool>, false)
              ProvidedStaticParameter("PreferAsync", typeof<bool>, false)
              ProvidedStaticParameter("SsrfProtection", typeof<bool>, true) ]

        t.AddXmlDoc
            """<summary>Statically typed OpenAPI provider.</summary>
               <param name='Schema'>Url or Path to OpenAPI schema file.</param>
               <param name='IgnoreOperationId'>Do not use `operationsId` and generate method names using `path` only. Default value `false`.</param>
               <param name='IgnoreControllerPrefix'>Do not parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`.</param>
               <param name='PreferNullable'>Provide `Nullable<_>` for not required properties, instead of `Option<_>`. Defaults value `false`.</param>
               <param name='PreferAsync'>Generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults value `false`.</param>
               <param name='SsrfProtection'>Enable SSRF protection (blocks HTTP and localhost). Set to false for development/testing. Default value `true`.</param>"""

        t.DefineStaticParameters(
            staticParams,
            fun typeName args ->
                let schemaPathRaw = unbox<string> args.[0]
                let ignoreOperationId = unbox<bool> args.[1]
                let ignoreControllerPrefix = unbox<bool> args.[2]
                let preferNullable = unbox<bool> args.[3]
                let preferAsync = unbox<bool> args.[4]
                let ssrfProtection = unbox<bool> args.[5]

                let cacheKey =
                    (schemaPathRaw, ignoreOperationId, ignoreControllerPrefix, preferNullable, preferAsync, ssrfProtection)
                    |> sprintf "%A"

                let addCache() =
                    lazy
                        let schemaData =
                            SchemaReader.readSchemaPath (not ssrfProtection) "" cfg.ResolutionFolder schemaPathRaw
                            |> Async.RunSynchronously

                        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

                        let (schema, diagnostic) = openApiReader.Read(schemaData)

                        if diagnostic.Errors.Count > 0 then
                            failwithf
                                "Schema parse errors:\n%s"
                                (diagnostic.Errors
                                 |> Seq.map(fun e -> $"%s{e.Message} @ %s{e.Pointer}")
                                 |> String.concat "\n")

                        let defCompiler = DefinitionCompiler(schema, preferNullable)

                        let opCompiler =
                            OperationCompiler(schema, defCompiler, ignoreControllerPrefix, ignoreOperationId, preferAsync)

                        opCompiler.CompileProvidedClients(defCompiler.Namespace)
                        let tys = defCompiler.Namespace.GetProvidedTypes()

                        let tempAsm = ProvidedAssembly()

                        let ty =
                            ProvidedTypeDefinition(tempAsm, ns, typeName, Some typeof<obj>, isErased = false, hideObjectMethods = true)

                        ty.AddXmlDoc("OpenAPI Provider for " + schemaPathRaw)
                        ty.AddMembers tys
                        tempAsm.AddTypes [ ty ]

                        ty

                try
                    OpenApiCache.providedTypes.GetOrAdd(cacheKey, addCache).Value
                with _ ->
                    OpenApiCache.providedTypes.Remove(cacheKey) |> ignore

                    OpenApiCache.providedTypes.GetOrAdd(cacheKey, addCache).Value
        )

        t

    do this.AddNamespace(ns, [ myParamType ])
