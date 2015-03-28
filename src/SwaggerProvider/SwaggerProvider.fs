namespace SwaggerProvider

open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System
open FSharp.Data
open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Compilers

[<TypeProvider>]
type public SwaggerProvider(cfg : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let ns = "SwaggerProvider"
    let asm = Assembly.GetExecutingAssembly()
    let tempAsmPath = IO.Path.ChangeExtension(IO.Path.GetTempFileName(), ".dll")
    let tempAsm = ProvidedAssembly tempAsmPath

    let t = ProvidedTypeDefinition(asm, ns, "SwaggerProvider", Some typeof<obj>, IsErased = false)
    let parameters = [ProvidedStaticParameter("Schema", typeof<string>)]

    do
        t.DefineStaticParameters(
            parameters=parameters,
            instantiationFunction=(fun typeName args ->
                let schemaPath = args.[0] :?> string

                // Create Swagger provider type
                let ty = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, IsErased = false)
                ty.AddXmlDoc ("Swagger.io Provider for " + schemaPath)

                let schema =
                    schemaPath
                    |> IO.File.ReadAllText
                    |> JsonValue.Parse
                    |> SwaggerSchema.Parse
                let defCompiler = DefinitionCompiler(schema)

                // Add all definitions
                ty.AddMember <| defCompiler.Compile()

                tempAsm.AddTypes [ty]
                ty
            ))
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        tempAsm.AddTypes [t]
        this.AddNamespace(ns, [t])


[<TypeProviderAssembly>]
do ()

