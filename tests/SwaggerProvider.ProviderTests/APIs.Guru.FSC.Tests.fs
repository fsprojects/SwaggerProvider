module APIsGuruFSC

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System.IO
open NUnit.Framework
open FsUnitTyped

let referencedAssemblies =
    let rootDir =  __SOURCE_DIRECTORY__ + "/../../bin/SwaggerProvider/"
    ["SwaggerProvider.Runtime.dll"
     "SwaggerProvider.DesignTime.dll"
     "SwaggerProvider.dll";]
    |> List.map (fun x->
        ["-r"; Path.Combine(rootDir, x)])
    |> List.concat

let scs = SimpleSourceCodeServices()


let toTestCase (url:string) =
    TestCaseData(url).SetName(sprintf "Compile schema %s" url)
let JsonSchemasSource = APIsGuru.JsonSchemas |> Array.map toTestCase


[<Test; TestCaseSource("JsonSchemasSource")>]
let ``Compile TP`` url =
    let tempFile = Path.GetTempFileName()
    let fs = Path.ChangeExtension(tempFile, ".fs")
    let dll = Path.ChangeExtension(tempFile, ".dll")

    File.WriteAllText(fs, sprintf """
    module TestModule
    open SwaggerProvider
    type ProvidedSwagger = SwaggerProvider<"%s">
    let instance = ProvidedSwagger()
    """ url)

    let errors, exitCode =
        scs.Compile(Array.ofList
           (["fsc.exe"; "-o"; dll; "-a"; fs] @ referencedAssemblies))

    for error in errors do
        eprintfn "%s" (error.ToString())

    [tempFile; fs; dll]
    |> List.filter File.Exists
    |> List.iter File.Delete

    exitCode |> shouldEqual 0