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

    let internal typedSwaggerProvider (context: Context) runtimeAssemlby =
        let asm = Assembly.LoadFrom runtimeAssemlby

        let swaggerProvider = ProvidedTypeDefinition(asm, NameSpace, "SwaggerProvider", Some typeof<obj>, IsErased = false)

        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("Headers", typeof<string>,"")
              ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)]

        //TODO: Add use operationID flag
        swaggerProvider.AddXmlDoc
            """<summary>Statically typed Swagger provider.</summary>
               <param name='Schema'>Url or Path to Swagger schema file.</param>
               <param name='Headers'>Headers that will be used to access the schema.</param>
               <param name='IgnoreOperationId'>IgnoreOperationId tells SwaggerProvider not to use `operationsId` and generate method names using `path` only. Default value `false`</param>"""

        let cache = new MemoryCache("SwaggerProvider")
        context.AddDisposable cache

        swaggerProvider.DefineStaticParameters(
                parameters=staticParams,
                instantiationFunction = (fun typeName args ->
                    let value = lazy (
                        let schemaPathRaw = args.[0] :?> string
                        let ignoreOperationId = args.[2] :?> bool

                        let schemaData =
                            match schemaPathRaw.StartsWith("http", true, null) with
                            | true  ->
                                let headersStr = args.[1] :?> string
                                let headers =
                                    headersStr.Split('|')
                                    |> Seq.choose (fun x ->
                                        let pair = x.Split('=')
                                        if (pair.Length = 2)
                                        then Some (pair.[0],pair.[1])
                                        else None
                                    )
                                Http.RequestString(schemaPathRaw, headers=headers)
                            | false ->
                                context.WatchFile schemaPathRaw
                                schemaPathRaw |> IO.File.ReadAllText

                        let schema =
                            if schemaData.Trim().StartsWith("{")
                            then JsonValue.Parse  schemaData |> JsonNodeAdapter |> Parser.parseSwaggerObject
                            else YamlParser.Parse schemaData |> YamlNodeAdapter |> Parser.parseSwaggerObject

                        // Create Swagger provider type
                        let baseTy = Some typeof<SwaggerProvider.Internal.ProvidedSwaggerBaseType>
                        let ty = ProvidedTypeDefinition(asm, NameSpace, typeName, baseTy, IsErased = false)
                        ty.AddXmlDoc ("Swagger.io Provider for " + schema.Host)

                        let ctor =
                            ProvidedConstructor(
                                [ProvidedParameter("host", typeof<string>, optionalValue = true) //schema.Host
                                 ProvidedParameter("headers", typeof<(string*string)[]>, optionalValue = true)//Array.empty<string*string>
                                 ProvidedParameter("customizeHttpRequest", typeof<Net.HttpWebRequest -> Net.HttpWebRequest>, optionalValue = true)], // id
                                InvokeCode = fun args ->
                                    match args with
                                    | [] -> failwith "Generated constructors should always pass the instance as the first argument!"
                                    | this::_ -> Expr.Coerce(this, ty))//<@@ () @@>)
                        ctor.BaseConstructorCall <-
                            let baseCtor = baseTy.Value.GetConstructors().[0]
                            fun args -> (baseCtor, args)
                        ty.AddMember ctor

                        let defCompiler = DefinitionCompiler(schema)
                        let opCompiler = OperationCompiler(schema, defCompiler)
                        ty.AddMembers <| opCompiler.CompilePaths(ignoreOperationId) // Add all operations
                        ty.AddMembers <| defCompiler.GetProvidedTypes() // Add all compiled types

                        let tempAsmPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".dll")
                        let tempAsm = ProvidedAssembly tempAsmPath
                        tempAsm.AddTypes [ty]

                        ty)
                    cache.GetOrAdd(typeName, value)
                ))
        swaggerProvider


