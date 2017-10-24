module APIsGuruFSCS

open Microsoft.FSharp.Compiler.SourceCodeServices
open System
open System.IO
open Expecto
open ProviderImplementation.ProvidedTypesTesting
open Fake

let assembliesList =
    let buildTarget name =
        Path.Combine(__SOURCE_DIRECTORY__, "../../bin/SwaggerProvider/", name)
        |> Path.GetFullPath
    [
        //yield typeof<FSharp.Core.AbstractClassAttribute>.Assembly.Location
        yield typeof<System.Int32>.Assembly.Location
        yield typeof<System.Net.CookieContainer>.Assembly.Location
        yield typeof<FSharp.Data.Http>.Assembly.Location
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
let referencedAssembliesFsi =
    // FSI needs .optdata and .sigdata files near FSharp.Core.dll
    Path.Combine(__SOURCE_DIRECTORY__, "../../packages/FSharp.Core/lib/net40/FSharp.Core.dll")
     :: assembliesList
    |> List.map (fun x -> sprintf "-r:%s" x)

let scs = new System.Threading.ThreadLocal<_>(fun () -> FSharpChecker.Create())

let testFsi = isNull <| Type.GetType("Mono.Runtime")
let fsiTest fs =
    let args = "--noframework" :: referencedAssembliesFsi |> List.toArray
    let isOk, msgs =
        executeBuildScriptWithArgsAndFsiArgsAndReturnMessages 
            fs [||] args false
    for msg in msgs do
        printfn "%s" msg.Message
    if not(isOk)
        then failwithf "fsiTest failed"

let compileTP url =
    let tempFile = Path.GetTempFileName()
    let fs = Path.ChangeExtension(tempFile, ".fs")
    let dll = Path.ChangeExtension(tempFile, ".dll")

    File.WriteAllText(fs, sprintf """
    module TestModule
    open SwaggerProvider
    type ProvidedSwagger = SwaggerProvider<"%s">
    let instance = ProvidedSwagger()
    #if INTERACTIVE
    System.Console.WriteLine("Hello from FSI: {0}", instance.Host)
    #endif
    """ url)

    try
        let errors, exitCode =
            scs.Value.Compile(Array.ofList
               (["fsc.exe"; "--noframework";
                 "-o"; dll; "-a"; fs
                ] @ referencedAssemblies))
            |> Async.RunSynchronously

        if exitCode <> 0 then
            failwithf "Compilation error:\n%s"
                (String.Join("\n", errors |> Array.map(fun x->x.ToString()) ))

        if testFsi then
            fsiTest fs
    finally
        [tempFile; fs; dll]
        |> List.filter File.Exists
        |> List.iter File.Delete

let focusedUrlPrefixes =
    [
        "https://api.apis.guru/v2/specs/oxforddictionaries.com/"
        "https://api.apis.guru/v2/specs/azure.com/arm-iothub/"
        "https://api.apis.guru/v2/specs/googleapis.com/compute/"
    ]

[<Tests>]
let compilerTests =
    List.ofArray  APIsGuru.JsonSchemas
    // Uncommend next line to run  integration tests only on `focusedUrlPrefixes` schemas
    //|> List.filter (fun url -> focusedUrlPrefixes |> Seq.exists url.StartsWith)
    |> List.map (fun url ->
        testCase
            (sprintf "Compile schema %s" url)
            (fun _ -> compileTP url)
       )
    |> testList "Integration/Compile TP"
    |> testSequenced
