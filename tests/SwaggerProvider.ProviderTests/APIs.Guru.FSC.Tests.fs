module APIsGuruFSCS

open Microsoft.FSharp.Compiler.SourceCodeServices
open System
open System.IO
open Expecto
open ProviderImplementation.ProvidedTypesTesting

let referencedAssemblies =
    let buildTarget name =
        Path.Combine(__SOURCE_DIRECTORY__, "../../bin/SwaggerProvider/", name)
    [
        yield typeof<System.Int32>.Assembly.Location
        yield typeof<FSharp.Core.AbstractClassAttribute>.Assembly.Location
        yield typeof<FSharp.Data.Http>.Assembly.Location
        yield typeof<System.Net.CookieContainer>.Assembly.Location
        yield buildTarget "SwaggerProvider.Runtime.dll"
        yield buildTarget "SwaggerProvider.dll"
    ]
    |> List.collect (fun path ->
        if not <| File.Exists(path)
            then failwithf "File not found '%s'" path
        ["-r"; path])

let scs = new System.Threading.ThreadLocal<_>(fun () -> FSharpChecker.Create())

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
        |> Async.RunSynchronously

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
    |> testSequenced
