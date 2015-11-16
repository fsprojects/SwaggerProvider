namespace SwaggerProvider

open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System
open FSharp.Data
open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open SwaggerProvider.Internal.Compilers

/// The Swagger Type Provider.
[<TypeProvider>]
type public SwaggerProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()

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

                let schemaData =
                    match schemaPathRaw.StartsWith("http", true, null) with
                    | true ->
                        Http.RequestString(schemaPathRaw)
                    | false ->
                        schemaPathRaw |> IO.File.ReadAllText

                let schema =
                    schemaData
                    |> JsonValue.Parse
                    |> JsonParser.parseSwaggerObject

                // Create Swagger provider type
                let ty = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, IsErased = false)
                ty.AddXmlDoc ("Swagger.io Provider for " + schema.Host)


                let defCompiler = DefinitionCompiler(schema)
                ty.AddMember <| defCompiler.Compile() // Add all definitions

                let opCompiler = OperationCompiler(schema, defCompiler, headers)
                ty.AddMembers <| opCompiler.Compile() // Add all operations

                let tempAsmPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".dll")
                let tempAsm = ProvidedAssembly tempAsmPath
                tempAsm.AddTypes [ty]

                ty
            ))
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        this.AddNamespace(ns, [t])


[<TypeProviderAssembly>]
do ()

