namespace SwaggerProvider

open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System
open System.Runtime.Caching
open FSharp.Data
open FSharp.Configuration.Helper
open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open SwaggerProvider.Internal.Compilers

module private SwaggerProviderConfig =
    let NameSpace = "SwaggerProvider"

    let internal typedSwaggerProvider (context: Context) =
        let asm = Assembly.GetExecutingAssembly()

        let swaggerProvider = ProvidedTypeDefinition(asm, NameSpace, "SwaggerProvider", Some typeof<obj>, IsErased = false)

        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("Headers", typeof<string>,"")]

        swaggerProvider.AddXmlDoc
            """<summary>Statically typed Swagger provider.</summary>
               <param name='Schema'>Url or Path to Swagger schema file.</param>
               <param name='Headers'>Headers that will be added to all HTTP requests.</param>"""

        let cache = new MemoryCache("SwaggerProvider")
        context.AddDisposable cache

        swaggerProvider.DefineStaticParameters(
                parameters=staticParams,
                instantiationFunction = (fun typeName args ->
                    let value = lazy (
                        let h = args.[1] :?> string
                        let headers = h.Split('|') |> Seq.filter (fun f -> f.Contains("=")) |> Seq.map (fun e ->
                            let pair = e.Split('=')
                            (pair.[0],pair.[1])
                            )

                        let schemaPathRaw = args.[0] :?> string

                        let schemaData =
                            match schemaPathRaw.StartsWith("http", true, null) with
                            | true ->
                                Http.RequestString(schemaPathRaw)
                            | false ->
                                context.WatchFile schemaPathRaw
                                schemaPathRaw |> IO.File.ReadAllText

                        let schema =
                            schemaData
                            |> JsonValue.Parse
                            |> JsonParser.parseSwaggerObject

                        // Create Swagger provider type
                        let ty = ProvidedTypeDefinition(asm, NameSpace, typeName, Some typeof<obj>, IsErased = false)
                        ty.AddXmlDoc ("Swagger.io Provider for " + schema.Host)


                        let defCompiler = DefinitionCompiler(schema)
                        ty.AddMember <| defCompiler.Compile() // Add all definitions

                        let opCompiler = OperationCompiler(schema, defCompiler, headers)
                        ty.AddMembers <| opCompiler.Compile() // Add all operations

                        let tempAsmPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".dll")
                        let tempAsm = ProvidedAssembly tempAsmPath
                        tempAsm.AddTypes [ty]

                        ty)
                    cache.GetOrAdd(typeName, value)
                ))
        swaggerProvider


/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerTypeProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()
    let context = new Context(this, cfg)
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        this.AddNamespace(
            SwaggerProviderConfig.NameSpace,
            [SwaggerProviderConfig.typedSwaggerProvider context])


[<TypeProviderAssembly>]
do ()

