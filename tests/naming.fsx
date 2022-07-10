#I "SwaggerProvider.Tests/bin/Release"
#r "SwaggerProvider.Runtime.dll"
#r "SwaggerProvider.DesignTime.dll"

open System
open System.IO
open Swagger.Parser
open SwaggerProvider.Internal
open SwaggerProvider.Internal.Compilers
open FSharp.Data.Runtime.NameUtils
open FSharp.Data

let loadSchema(url: string) =
    try
        if url.StartsWith("http") then
            Http.RequestString url
        else
            File.ReadAllText url
        |> SwaggerParser.parseJson
        |> Parsers.parseSwaggerObject
        |> Some
    with :? Net.WebException ->
        None

let analyze schemas =
    let schemas =
        schemas
        |> Array.choose(fun url ->
            printfn "Loading '%s'..." url
            loadSchema url |> Option.map(fun x -> (url, x)))

    let toCsv(arr: string[]) =
        sprintf "\"%s\"" <| String.Join("\",\"", arr)

    let definitions =
        schemas
        |> Array.collect(fun (url, schema) ->
            let scope = UniqueNameGenerator()

            schema.Definitions
            |> Array.map(fun (name, _) ->
                let tyName =
                    name.Substring("#/definitions/".Length)
                    |> nicePascalName
                    |> scope.MakeUnique

                toCsv [| name; tyName; url |]))

    let paths =
        schemas
        |> Array.collect(fun (url, schema) ->
            let scope1 = UniqueNameGenerator()
            let scope2 = UniqueNameGenerator()

            schema.Paths
            |> Array.map(fun path ->
                toCsv [|
                    path.OperationId
                    scope1.MakeUnique
                    <| OperationCompiler.GetMethodNameCandidate path true
                    scope2.MakeUnique
                    <| OperationCompiler.GetMethodNameCandidate path false
                    path.Path
                    url
                |]))

    definitions, paths

#r "SwaggerProvider.Tests.exe"

let types, ops = analyze APIsGuru.schemaUrls

let getPath name =
    Path.Combine(__SOURCE_DIRECTORY__, name)

File.WriteAllLines(getPath "types.csv", types)
File.WriteAllLines(getPath "opps.csv", ops)
