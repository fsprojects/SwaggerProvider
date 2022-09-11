namespace SwaggerProvider

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Swagger
open SwaggerProvider.Internal
open SwaggerProvider.Internal.v2.Parser
open SwaggerProvider.Internal.v2.Compilers

/// The Swagger Type Provider.
[<TypeProvider; Obsolete("Use OpenApiClientTypeProvider when possible, it supports v2 & v3 schema formats.")>]
type public SwaggerTypeProvider(cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces
        (
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
            ProvidedTypeDefinition(asm, ns, "SwaggerClientProvider", Some typeof<obj>, isErased = false)

        let staticParams = [
            ProvidedStaticParameter("Schema", typeof<string>)
            ProvidedStaticParameter("Headers", typeof<string>, "")
            ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)
            ProvidedStaticParameter("IgnoreControllerPrefix", typeof<bool>, true)
            ProvidedStaticParameter("PreferNullable", typeof<bool>, false)
            ProvidedStaticParameter("PreferAsync", typeof<bool>, false)
        ]

        t.AddXmlDoc
            """<summary>Statically typed Swagger provider.</summary>
               <param name='Schema'>Url or Path to Swagger schema file.</param>
               <param name='Headers'>HTTP Headers requiried to access the schema.</param>
               <param name='IgnoreOperationId'>Do not use `operationsId` and generate method names using `path` only. Default value `false`.</param>
               <param name='IgnoreControllerPrefix'>Do not parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`.</param>
               <param name='PreferNullable'>Provide `Nullable<_>` for not required properties, instead of `Option<_>`. Defaults value `false`.</param>
               <param name='PreferAsync'>Generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults value `false`.</param>"""

        t.DefineStaticParameters(
            staticParams,
            fun typeName args ->
                let schemaPath =
                    let schemaPathRaw = unbox<string> args.[0]
                    SchemaReader.getAbsolutePath cfg.ResolutionFolder schemaPathRaw

                let headersStr = unbox<string> args.[1]
                let ignoreOperationId = unbox<bool> args.[2]
                let ignoreControllerPrefix = unbox<bool> args.[3]
                let preferNullable = unbox<bool> args.[4]
                let preferAsync = unbox<bool> args.[5]

                let cacheKey =
                    (schemaPath, headersStr, ignoreOperationId, ignoreControllerPrefix, preferNullable, preferAsync)
                    |> sprintf "%A"

                let addCache() =
                    lazy
                        let schemaData =
                            SchemaReader.readSchemaPath headersStr schemaPath
                            |> Async.RunSynchronously

                        let schema = SwaggerParser.parseSchema schemaData

                        let defCompiler = DefinitionCompiler(schema, preferNullable)

                        let opCompiler =
                            OperationCompiler(schema, defCompiler, ignoreControllerPrefix, ignoreOperationId, preferAsync)

                        opCompiler.CompileProvidedClients(defCompiler.Namespace)
                        let tys = defCompiler.Namespace.GetProvidedTypes()

                        let tempAsm = ProvidedAssembly()

                        let ty =
                            ProvidedTypeDefinition(tempAsm, ns, typeName, Some typeof<obj>, isErased = false, hideObjectMethods = true)

                        ty.AddXmlDoc("Swagger Provider for " + schemaPath)
                        ty.AddMembers tys
                        tempAsm.AddTypes [ ty ]

                        ty

                Cache.providedTypes.GetOrAdd(cacheKey, addCache).Value
        )

        t

    do this.AddNamespace(ns, [ myParamType ])
