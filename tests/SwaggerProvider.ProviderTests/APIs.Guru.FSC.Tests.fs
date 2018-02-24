module APIsGuruFSCS

open Microsoft.FSharp.Compiler.SourceCodeServices
open System
open System.IO
open Expecto
open Fake

let assembliesList =
    let buildTarget name =
        Path.Combine(__SOURCE_DIRECTORY__, "../../bin/SwaggerProvider/", name)
        |> Path.GetFullPath
    [
        //yield typeof<FSharp.Core.AbstractClassAttribute>.Assembly.Location
        yield typeof<System.Int32>.Assembly.Location
        yield typeof<System.Net.CookieContainer>.Assembly.Location
        yield typeof<System.Net.Http.HttpRequestMessage>.Assembly.Location
        yield buildTarget "SwaggerProvider.Runtime.dll"
        yield buildTarget "SwaggerProvider.dll"
    ]
let referencedAssemblies =
    typeof<FSharp.Core.AbstractClassAttribute>.Assembly.Location
     :: assembliesList
    |> List.collect (fun path ->
        if not <| File.Exists(path)
            then failwithf "File not found '%s'" path
        ["-r"; path])

let scs = new System.Threading.ThreadLocal<_>(fun () -> FSharpChecker.Create())

let testTemplate url testBodyFunc =
    let tempFile = Path.GetTempFileName()
    let fs = Path.ChangeExtension(tempFile, ".fs")
    let dll = Path.ChangeExtension(tempFile, ".dll")

    File.WriteAllText(fs, sprintf """
    module TestModule
    open SwaggerProvider
    type ProvidedSwagger = SwaggerProvider<"%s">
    let instance = ProvidedSwagger.Client()
    #if INTERACTIVE
    System.Console.WriteLine("Hello from FSI: {0}", instance.Host)
    #endif
    """ url)

    try
        testBodyFunc fs dll
    finally
        [tempFile; fs; dll]
        |> List.filter File.Exists
        |> List.iter File.Delete

let compileTP fs dll =
    let errors, exitCode =
        scs.Value.Compile(Array.ofList
           (["fsc.exe"; "--noframework";
             "-o"; dll; "-a"; fs
            ] @ referencedAssemblies))
        |> Async.RunSynchronously

    if exitCode <> 0 then
        failwithf "Compilation error:\n%s"
            (String.Join("\n", errors |> Array.map(fun x->x.ToString()) ))

[<Tests>]
let compilerTests =
    List.ofArray  APIsGuru.JsonSchemas
    |> List.map (fun url ->
        testCase
            (sprintf "Compile schema %s" url)
            (fun _ -> testTemplate url compileTP)
       )
    |> testList "Integration/Compile TP"
    |> testSequenced


let referencedAssembliesFsi =
    // FSI needs .optdata and .sigdata files near FSharp.Core.dll
    Path.Combine(__SOURCE_DIRECTORY__, "../../packages/FSharp.Core/lib/net40/FSharp.Core.dll")
     :: assembliesList
    |> List.map (fun x -> sprintf "-r:%s" x)

let fsiTest fs _ =
    let args = "--noframework" :: referencedAssembliesFsi |> List.toArray
    let isOk, msgs =
        executeBuildScriptWithArgsAndFsiArgsAndReturnMessages 
            fs [||] args false
    for msg in msgs do
        printfn "%s" msg.Message
    if not(isOk)
        then failwithf "fsiTest failed"

let testFsi = isNull <| Type.GetType("Mono.Runtime")
[<Tests>]
let fsiTests =
    APIsGuru.JsonSchemas
    |> APIsGuru.shrink 30
    |> List.ofArray
    |> List.distinct
    |> List.choose (fun url ->
        if testFsi then
            testCase
                (sprintf "Compile schema %s" url)
                (fun _ -> testTemplate url fsiTest)
            |> Some
        else None
       )
    |> testList "Integration/Load TP in FSI"
    |> testSequenced