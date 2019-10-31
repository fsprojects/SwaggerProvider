namespace SwaggerProvider

open System
open System.Reflection
open System.Net.Http
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Swagger
open SwaggerProvider.Internal.v3.Compilers

module Cache =
    let providedTypes = Caching.createInMemoryCache (TimeSpan.FromMinutes 5.)

/// The Open API Provider.
[<TypeProvider>]
type public OpenApiClientTypeProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg, assemblyReplacementMap=[("SwaggerProvider.DesignTime", "SwaggerProvider.Runtime")], addDefaultProbingLocation=true)

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<ProvidedApiClientBase>.Assembly.GetName().Name = asm.GetName().Name)

    let myParamType =
        let t = ProvidedTypeDefinition(asm, ns, "OpenApiClientProvider", Some typeof<obj>, isErased=false)
        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)
              ProvidedStaticParameter("IgnoreControllerPrefix", typeof<bool>, true)
              ProvidedStaticParameter("ProvideNullable", typeof<bool>, false)
              ProvidedStaticParameter("PreferAsync", typeof<bool>, false)]
        t.AddXmlDoc
            """<summary>Statically typed OpenAPI Provider.</summary>
               <param name='Schema'>Url or Path to OpenAPI schema file.</param>
               <param name='IgnoreOperationId'>IgnoreOperationId tells OpenApiClientProvider not to use `operationsId` and generate method names using `path` only. Default value `false`</param>
               <param name='IgnoreControllerPrefix'>IgnoreControllerPrefix tells OpenApiClientProvider not to parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`</param>
               <param name='ProvideNullable'>Provide `Nullable<_>` for not required properties, instead of `Option<_>`</param>
               <param name='PreferAsync'>PreferAsync tells the OpenApiClientProvider to generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults to `false`</param>"""

        t.DefineStaticParameters(
            staticParams,
            fun typeName args ->
                let schemaPathRaw = unbox<string> args.[0]
                let ignoreOperationId = unbox<bool>  args.[1]
                let ignoreControllerPrefix = unbox<bool>  args.[2]
                let provideNullable = unbox<bool>  args.[3]
                let asAsync = unbox<bool>  args.[4]

                let cacheKey =
                    (schemaPathRaw, ignoreOperationId, ignoreControllerPrefix, provideNullable, asAsync)
                    |> sprintf "%A"

                let tys =
                    match Cache.providedTypes.TryRetrieve(cacheKey) with
                    | Some(x) -> x
                    | None ->
                        let schemaData =
                            match schemaPathRaw.StartsWith("http", true, null) with
                            | true  ->
                                let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)
                                // using a custom handler means that we can set the default credentials.
                                use handler = new HttpClientHandler(UseDefaultCredentials = true)
                                use client = new HttpClient(handler)
                                async {
                                    let! response = client.SendAsync(request) |> Async.AwaitTask
                                    return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
                                } |> Async.RunSynchronously
                            | false ->
                                schemaPathRaw |> IO.File.ReadAllText
                        let openApiReader = Microsoft.OpenApi.Readers.OpenApiStringReader()

                        let (schema, diagnostic) = openApiReader.Read(schemaData)
                        if diagnostic.Errors.Count > 0 then
                            failwithf "Schema parse errors: %s"
                                (diagnostic.Errors
                                 |> Seq.map (fun e -> e.Message)
                                 |> String.concat ";")

                        let defCompiler = DefinitionCompiler(schema, provideNullable)
                        let opCompiler = OperationCompiler(schema, defCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync)
                        opCompiler.CompileProvidedClients(defCompiler.Namespace)
                        let tys = defCompiler.Namespace.GetProvidedTypes()

                        Cache.providedTypes.Set(cacheKey, tys)
                        tys

                let tempAsm = ProvidedAssembly()
                let ty = ProvidedTypeDefinition(tempAsm, ns, typeName, Some typeof<obj>, isErased = false, hideObjectMethods = true)
                ty.AddXmlDoc ("OpenAPI Provider for " + schemaPathRaw)

                ty.AddMembers tys
                tempAsm.AddTypes [ty]
                ty
        )
        t
    do
        this.AddNamespace(ns, [myParamType])
