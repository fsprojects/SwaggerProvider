#r @"paket:
source https://nuget.org/api/v2
framework net6.0
nuget FSharp.Core
nuget Fake.Core.Target
nuget Fake.Core.Process
nuget Fake.Core.ReleaseNotes
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.Paket
nuget Fake.DotNet.FSFormatting
nuget Fake.Tools.Git
nuget Fake.Api.GitHub //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake
open Fake.Core.TargetOperators
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.Tools
open System.IO

Target.initEnvironment()

// --------------------------------------------------------------------------------------

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "F# Type Provider for Swagger & Open API"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
// The name of the project on GitHub
let gitName = "SwaggerProvider"

// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = ReleaseNotes.load "docs/RELEASE_NOTES.md"

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
    let fileName = "src/Common/AssemblyInfo.fs"

    AssemblyInfoFile.createFSharp fileName [
        AssemblyInfo.Title gitName
        AssemblyInfo.Product gitName
        AssemblyInfo.Description description
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion
    ])

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    !! "**/**/bin/" |> Shell.cleanDirs
    //!! "**/**/obj/" |> Shell.cleanDirs

    Shell.cleanDirs [ "bin"; "temp" ]

    try
        File.Delete("swaggerlog")
    with _ ->
        ())

Target.create "CleanDocs" (fun _ -> Shell.cleanDirs [ "docs/output" ])

// --------------------------------------------------------------------------------------
// Build library & test project

let dotnet cmd args =
    let result = DotNet.exec id cmd args

    if not result.OK then
        failwithf "Failed: %A" result.Errors

Target.create "Build" (fun _ -> dotnet "build" "SwaggerProvider.sln -c Release")

let webApiInputStream = StreamRef.Empty

Target.create "StartServer" (fun _ ->
    Target.activateFinal "StopServer"

    CreateProcess.fromRawCommandLine "dotnet" "tests/Swashbuckle.WebApi.Server/bin/Release/net6.0/Swashbuckle.WebApi.Server.dll"
    |> CreateProcess.withStandardInput(CreatePipe webApiInputStream)
    |> Proc.start
    |> ignore

    // We need delay to guarantee that server is bootstrapped
    System.Threading.Thread.Sleep(2000))

Target.createFinal "StopServer" (fun _ ->
    // Write something to input stream to stop server
    try
        webApiInputStream.Value.Write([| 0uy |], 0, 1)
    with e ->
        printfn "%s" e.Message
//Process.killAllByName "dotnet"
)

Target.create "BuildTests" (fun _ -> dotnet "build" "SwaggerProvider.TestsAndDocs.sln -c Release")

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

let runTests assembly =
    dotnet "test" $"{assembly} -c Release --no-build"

Target.create "RunUnitTests" (fun _ -> runTests "tests/SwaggerProvider.Tests/bin/Release/net6.0/SwaggerProvider.Tests.dll")

Target.create "RunIntegrationTests" (fun _ -> runTests "tests/SwaggerProvider.ProviderTests/bin/Release/net6.0/SwaggerProvider.ProviderTests.dll")

Target.create "RunTests" ignore

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    Paket.pack(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes
        }))

Target.create "PublishNuget" (fun _ ->
    Paket.push(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            WorkingDir = "bin"
        }))

// --------------------------------------------------------------------------------------
// Generate the documentation

Target.create "BrowseDocs" (fun _ ->
    CreateProcess.fromRawCommandLine "dotnet" "serve -o -d ./docs"
    |> (Proc.run >> ignore))

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "Release" (fun _ ->
    // not fully converted from  FAKE 4

    // StageAll ""
    // Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    // Branches.push ""

    // Branches.tag "" release.NugetVersion
    // Branches.pushTag "" "origin" release.NugetVersion

    // // release on github
    // createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    // |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // // TODO: |> uploadFile "PATH_TO_FILE"
    // |> releaseDraft
    // |> Async.RunSynchronously

    // using simplified FAKE 5 release for now

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Git.Branches.push ""

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion)

Target.create "BuildPackage" ignore

let sourceFiles =
    !! "**/*.fs" ++ "**/*.fsx"
    -- "packages/**/*.*"
    -- "paket-files/**/*.*"
    -- ".fake/**/*.*"
    -- "**/obj/**/*.*"
    -- "**/AssemblyInfo.fs"

Target.create "Format" (fun _ ->
    let result =
        sourceFiles
        |> Seq.map(sprintf "\"%s\"")
        |> String.concat " "
        |> DotNet.exec id "fantomas"

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages)

Target.create "CheckFormat" (fun _ ->
    let result =
        sourceFiles
        |> Seq.map(sprintf "\"%s\"")
        |> String.concat " "
        |> sprintf "%s --check"
        |> DotNet.exec id "fantomas"

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, run `dotnet fake build -t Format` to format them"
    else
        Trace.logf "Errors while formatting: %A" result.Errors
        failwith "Unknown errors while formatting")

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

// https://github.com/fsharp/FAKE/issues/2283
let skipTests = Environment.environVarAsBoolOrDefault "skipTests" false


"Clean"
==> "AssemblyInfo"
==> "CheckFormat"
==> "Build"
==> "RunUnitTests"
==> "StartServer"
==> "BuildTests"
=?> ("RunIntegrationTests", not skipTests)
==> "StopServer"
==> "RunTests"
==> "NuGet"
==> "All"
==> "BuildPackage"
==> "PublishNuget"
==> "Release"

Target.runOrDefault "BuildPackage"
