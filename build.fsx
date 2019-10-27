#r @"paket:
source https://nuget.org/api/v2
framework netstandard2.0
nuget Fake.Core.Target
nuget Fake.Core.Process
nuget Fake.Core.ReleaseNotes 
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.Paket
nuget Fake.DotNet.Testing.Expecto 
nuget Fake.DotNet.FSFormatting 
nuget Fake.Tools.Git
nuget Fake.Api.GitHub //"

#if !FAKE
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard" // Temp fix for https://github.com/fsharp/FAKE/issues/1985
#endif

open Fake 
open Fake.Core.TargetOperators
open Fake.Core 
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.Tools
open Fake.Tools.Git
open System
open System.IO

Target.initEnvironment()

// --------------------------------------------------------------------------------------

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "F# Type Provider for Swagger & Open API"
// Pattern specifying assemblies to be tested using Expecto
let testAssemblies = "tests/**/bin/Release" </> "**" </> "*Tests*.exe"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
// The name of the project on GitHub
let gitName = "SwaggerProvider"

// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
    let fileName = "src/Common/AssemblyInfo.fs"
    AssemblyInfoFile.createFSharp fileName
      [ AssemblyInfo.Title gitName
        AssemblyInfo.Product gitName
        AssemblyInfo.Description description
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion ]
)

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"]
    try File.Delete("swaggerlog") with | _ -> ()
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target.create "Build" (fun _ ->
    DotNet.exec id "build" "SwaggerProvider.sln -c Release" |> ignore
)

let webApiInputStream = StreamRef.Empty
Target.create "StartServer" (fun _ ->
    Target.activateFinal "StopServer"

    CreateProcess.fromRawCommandLine "dotnet" "tests/Swashbuckle.WebApi.Server/bin/Release/netcoreapp2.1/Swashbuckle.WebApi.Server.dll"
    |> CreateProcess.withStandardInput (CreatePipe webApiInputStream)
    |> Proc.start
    |> ignore
    
    // We need delay to guarantee that server is bootstrapped
    System.Threading.Thread.Sleep(2000)
)

Target.createFinal "StopServer" (fun _ ->
    // Write something to input stream to stop server
    webApiInputStream.Value.Write([|0uy|],0,1)
    //Process.killAllByName "dotnet"
)

Target.create "BuildTests" (fun _ ->
    DotNet.exec id "build" "SwaggerProvider.TestsAndDocs.sln -c Release" |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target.create "RunUnitTests" (fun _ ->
    // !! ("tests/SwaggerProvider.Tests/bin/Release" </> "**" </> "*Tests*.exe")
    // |> Expecto.run  (fun p ->
    //     { p with Summary = true})

    let xs = ["tests/SwaggerProvider.Tests/bin/Release/net461/SwaggerProvider.Tests.exe";
              "--fail-on-focused-tests"; "--sequenced"; "--version"]
    let cmd, parameters =
        if Environment.isWindows 
        then List.head xs, List.tail xs
        else "mono", xs

    CreateProcess.fromRawCommand cmd parameters
    |> CreateProcess.redirectOutput
    |> CreateProcess.withOutputEventsNotNull Trace.trace Trace.traceError
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore 
)

Target.create "RunIntegrationTests" (fun _ ->
    // !! testAssemblies
    // |> Expecto.run (fun p ->
    //     { p with Filter = "Integration/"})

    let xs = ["tests/SwaggerProvider.ProviderTests/bin/Release/net461/SwaggerProvider.ProviderTests.exe";
              "--fail-on-focused-tests"; "--sequenced"; "--version"]
    let cmd, parameters =
        if Environment.isWindows 
        then List.head xs, List.tail xs
        else "mono", xs

    CreateProcess.fromRawCommand cmd parameters
    |> CreateProcess.redirectOutput
    |> CreateProcess.withOutputEventsNotNull Trace.trace Trace.traceError
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore 
)

Target.create "RunTests" ignore

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    Paket.pack(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes})
)

Target.create "PublishNuget" (fun _ ->
    Paket.push(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            WorkingDir = "bin" })
)

// --------------------------------------------------------------------------------------
// Generate the documentation

// module Fake =
//     let fakePath = "packages" </> "build" </> "FAKE" </> "tools" </> "FAKE.exe"
//     let fakeStartInfo script workingDirectory args fsiargs environmentVars =
//         (fun (info: Diagnostics.ProcessStartInfo) ->
//             info.FileName <- Path.GetFullPath fakePath
//             info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
//             info.WorkingDirectory <- workingDirectory
//             let setVar k v = info.EnvironmentVariables.[k] <- v
//             for (k, v) in environmentVars do setVar k v
//             setVar "MSBuild" msBuildExe
//             setVar "GIT" CommandHelper.gitPath
//             setVar "FSI" fsiPath)

//     /// Run the given buildscript with FAKE.exe
//     let executeFAKEWithOutput workingDirectory script fsiargs envArgs =
//         let exitCode =
//             ExecProcessWithLambdas
//                 (fakeStartInfo script workingDirectory "" fsiargs envArgs)
//                 TimeSpan.MaxValue false ignore ignore
//         System.Threading.Thread.Sleep 1000
//         exitCode

// Target.create "BrowseDocs" (fun _ ->
//     let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "BrowseDocs"]
//     if exit <> 0 then failwith "Browsing documentation failed"
// )

// Target.create "GenerateDocs" (fun _ ->
//     let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "GenerateDocs"]
//     if exit <> 0 then failwith "Generating documentation failed"
// )

// Target.create "PublishDocs" (fun _ ->
//     let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "PublishDocs"]
//     if exit <> 0 then failwith "Publishing documentation failed"
// )

// Target.create "PublishStaticPages" (fun _ ->
//     let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "PublishStaticPages"]
//     if exit <> 0 then failwith "Publishing documentation failed"
// )

// --------------------------------------------------------------------------------------
// Release Scripts

//#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
//open Octokit

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
    Git.Branches.pushTag "" "origin" release.NugetVersion
)

Target.create "BuildPackage" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

// https://github.com/fsharp/FAKE/issues/2283
let skipTests = Environment.environVarAsBoolOrDefault "skipTests" false


"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunUnitTests"
  ==> "StartServer"
  ==> "BuildTests"
  =?> ("RunIntegrationTests", not skipTests)
  ==> "StopServer"
  ==> "RunTests"
  //=?> ("GenerateDocs", BuildServer.isLocalBuild)
  ==> "NuGet"
  ==> "All"
  ==> "BuildPackage"
  ==> "PublishNuget"
  ==> "Release"

Target.runOrDefault "BuildPackage"
