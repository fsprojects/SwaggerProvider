namespace SwaggerProvider

open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System
open FSharp.Data
open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Compilers

/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()
    let tempAsmPath = IO.Path.ChangeExtension(IO.Path.GetTempFileName(), ".dll")
    let tempAsm = ProvidedAssembly tempAsmPath

    let t = ProvidedTypeDefinition(asm, ns, "SwaggerProvider", Some typeof<obj>, IsErased = false)

    let parameters = [ProvidedStaticParameter("Schema", typeof<string>)
                      ProvidedStaticParameter("Headers", typeof<string>,"")]

    do
        t.DefineStaticParameters(
            parameters=parameters,
            instantiationFunction = (fun typeName args ->
                let h = args.[1] :?> string
                let headers = h.Split(';') |> Seq.filter (fun f -> f.Contains(",")) |> Seq.map (fun e -> 
                    let pair = e.Split(',')
                    (pair.[0],pair.[1])
                    )                    

                let schemaPathRaw = args.[0] :?> string

                let schemaPath = 
                    match schemaPathRaw.StartsWith("http", true, null) with
                    | true -> 
                        let root =  __SOURCE_DIRECTORY__
                        let swaggerFilePath = root + @"/../../tests/SwaggerProvider.Tests/Schemas/swaggerSchema.json"
                        // Download File
                        System.Net.ServicePointManager.ServerCertificateValidationCallback <-
                          System.Net.Security.RemoteCertificateValidationCallback(fun _ _ _ _ -> true)
                        (new System.Net.WebClient()).DownloadFile((schemaPathRaw), swaggerFilePath )
                        swaggerFilePath
                    | false ->
                        schemaPathRaw

                // Create Swagger provider type
                let ty = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, IsErased = false)
                ty.AddXmlDoc ("Swagger.io Provider for " + schemaPathRaw.Substring(schemaPathRaw.LastIndexOfAny [|'/';'\\'|]))

                let schema =
                    schemaPath
                    |> IO.File.ReadAllText
                    |> JsonValue.Parse
                    |> SwaggerSchema.Parse

                let defCompiler = DefinitionCompiler(schema)
                ty.AddMember <| defCompiler.Compile() // Add all definitions

                let opCompiler = OperationCompiler(schema, defCompiler, headers)
                ty.AddMembers <| opCompiler.Compile() // Add all operations

                tempAsm.AddTypes [ty]
                ty
            ))
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        tempAsm.AddTypes [t]
        this.AddNamespace(ns, [t])


[<TypeProviderAssembly>]
do ()

