module APIsGuruFSCS

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System
open System.IO
open Expecto

let referencedAssemblies =
    let rootDir =  __SOURCE_DIRECTORY__ + "/../../bin/SwaggerProvider/"
    ["SwaggerProvider.Runtime.dll"
     "SwaggerProvider.DesignTime.dll"
     "SwaggerProvider.dll";]
    |> List.map (fun x->
        ["-r"; Path.Combine(rootDir, x)])
    |> List.concat

let scs = SimpleSourceCodeServices()

let compileTP url =
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

    [tempFile; fs; dll]
    |> List.filter File.Exists
    |> List.iter File.Delete

    if exitCode <> 0 then
        failwithf "Compilation error:\n%s"
            (String.Join("\n", errors |> Array.map(fun x->x.ToString()) ))


[<Tests>]
let compilerTests =
    List.ofArray  APIsGuru.JsonSchemas
    |> List.map (fun url ->
        testCase
            (sprintf "Compile schema %s" url)
            (fun _ -> compileTP url)
       )
    |> testList "Integration/Compile TP"