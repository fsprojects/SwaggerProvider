namespace SwaggerProvider

open System
open System.Reflection
open System.Net.Http
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.OpenApi.Readers
open SwaggerProvider.Internal.Compilers
open Swagger

//module Handlers =

  // let logError event (ex: exn) =
  //   let ex =
  //     match ex with
  //     | :? TypeInitializationException as typInit -> typInit.InnerException
  //     | _ -> ex
  //   Logging.logf "[%s]\t%s: %s\n%s" event (ex.GetType().Name) ex.Message ex.StackTrace
  // let logResolve kind (args: ResolveEventArgs) = Logging.logf "[%s]\t%s on behalf of %s" kind args.Name args.RequestingAssembly.FullName

module Cache =
    let providedTypes = Caching.createInMemoryCache (TimeSpan.FromMinutes 5.)


/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerTypeProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg, assemblyReplacementMap=[("SwaggerProvider.DesignTime", "SwaggerProvider.Runtime")], addDefaultProbingLocation=true)

    // static do
    //   AppDomain.CurrentDomain.FirstChanceException.Add(fun args -> Handlers.logError "FirstChanceException" args.Exception)
    //   AppDomain.CurrentDomain.UnhandledException.Add(fun args -> Handlers.logError "UnhandledException" (args.ExceptionObject :?> exn))

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<SwaggerApiClientBase>.Assembly.GetName().Name = asm.GetName().Name)

    let myParamType =
        let t = ProvidedTypeDefinition(asm, ns, "SwaggerProvider", Some typeof<obj>, isErased=false)
        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("Headers", typeof<string>, "")
              ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)
              ProvidedStaticParameter("IgnoreControllerPrefix", typeof<bool>, true)
              ProvidedStaticParameter("ProvideNullable", typeof<bool>, false)
              ProvidedStaticParameter("PreferAsync", typeof<bool>, false)]
        t.AddXmlDoc
            """<summary>Statically typed Swagger provider.</summary>
               <param name='Schema'>Url or Path to Swagger schema file.</param>
               <param name='Headers'>Headers that will be used to access the schema.</param>
               <param name='IgnoreOperationId'>IgnoreOperationId tells SwaggerProvider not to use `operationsId` and generate method names using `path` only. Default value `false`</param>
               <param name='IgnoreControllerPrefix'>IgnoreControllerPrefix tells SwaggerProvider not to parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`</param>
               <param name='ProvideNullable'>Provide `Nullable<_>` for not required properties, instread of `Option<_>`</param>
               <param name='PreferAsync'>PreferAsync tells the SwaggerProvider to generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults to `false`</param>"""

        t.DefineStaticParameters(
          staticParams,
          fun typeName args ->
            let schemaPathRaw = unbox<string> args.[0]
            let headersStr = unbox<string> args.[1]
            let ignoreOperationId = unbox<bool>  args.[2]
            let ignoreControllerPrefix = unbox<bool>  args.[3]
            let provideNullable = unbox<bool>  args.[4]
            let asAsync = unbox<bool>  args.[5]

            let cacheKey =(schemaPathRaw, headersStr, ignoreOperationId, ignoreControllerPrefix, provideNullable, asAsync)

            let tys =
                match Cache.providedTypes.TryRetrieve(cacheKey) with
                | Some(x) -> x
                | None ->
                    let schemaData =
                        match schemaPathRaw.StartsWith("http", true, null) with
                        | true  ->
                            let headers =
                                headersStr.Split('|')
                                |> Seq.choose (fun x ->
                                    let pair = x.Split('=')
                                    if (pair.Length = 2)
                                    then Some (pair.[0],pair.[1])
                                    else None
                                )
                            let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)
                            for (name, value) in headers do
                                request.Headers.TryAddWithoutValidation(name, value) |> ignore
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
            ty.AddXmlDoc ("Swagger Provider for " + schemaPathRaw)

            ty.AddMembers tys
            tempAsm.AddTypes [ty]
            ty
        )
        t
    do
        this.AddNamespace(ns, [myParamType])
