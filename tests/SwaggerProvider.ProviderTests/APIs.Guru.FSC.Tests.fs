module APIsGuruFSCS

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System
open System.IO
open Expecto

let referencedAssemblies =
    let buildTarget name =
        Path.Combine(__SOURCE_DIRECTORY__, "../../bin/SwaggerProvider/", name)
    [
        Path.Combine(__SOURCE_DIRECTORY__, "../../packages/test/FSharp.Core/lib/net45/FSharp.Core.dll")
        buildTarget "SwaggerProvider.Runtime.dll"
        buildTarget "SwaggerProvider.dll"
    ]
    |> List.collect (fun path ->
        if not <| File.Exists(path)
            then failwithf "File not found '%s'" path
        ["-r"; path])

let scs = new System.Threading.ThreadLocal<_>(fun () -> SimpleSourceCodeServices())

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
        scs.Value.Compile(Array.ofList
           (["fsc.exe"; "--noframework";
             "-o"; dll; "-a"; fs
            ] @ referencedAssemblies))

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
    //|> testSequenced
