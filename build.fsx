// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

// Added to allow building the script from F# interactive. If the build fails F#
// interactive allows you to review the full log, unlike the Windows Command Prompt.
#if INTERACTIVE
System.IO.Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)
#endif

#r "paket: groupref Build //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet.Testing
open Fake.Tools

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
    try System.IO.File.Delete("swaggerlog") with | _ -> ()
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

let dotnetSdk = lazy DotNet.install DotNet.Versions.FromGlobalJson
let inline withWorkDir wd = DotNet.Options.lift dotnetSdk.Value >> DotNet.Options.withWorkingDirectory wd
let inline dotnetSimple arg = DotNet.Options.lift dotnetSdk.Value arg

Target.create "Build" (fun _ ->
    DotNet.exec dotnetSimple "build" "SwaggerProvider.sln -c Release" |> ignore
)

Target.create "StartServer" (fun _ ->
    CreateProcess.fromRawCommandLine "tests/Swashbuckle.OWIN.Server/bin/Release/net461/Swashbuckle.OWIN.Server.exe" ""
    |> Proc.start // start with the above configuration
    |> ignore // ignore exit code
    // Process.start (fun p ->
    //     { p with
    //         FileName = "tests/Swashbuckle.OWIN.Server/bin/Release/net461/Swashbuckle.OWIN.Server.exe"
    //     })
    System.Threading.Thread.Sleep(2000)
)

Target.create "BuildTests" (fun _ ->
    DotNet.exec dotnetSimple "build" "SwaggerProvider.TestsAndDocs.sln" |> ignore
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
    !! testAssemblies
    |> Expecto.run (fun p ->
        { p with Filter = "Integration/"})
)

Target.createFinal "StopServer" (fun _ ->
    Process.killAllByName "Swashbuckle.OWIN.Server"
)

Target.create "RunTests" ignore

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    Paket.pack(fun p ->
        { p with
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes})
)

Target.create "PublishNuget" (fun _ ->
    Paket.push(fun p ->
        { p with
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

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunUnitTests"
  ==> "StartServer"
  //==> "BuildTests"
  //=?> ("RunIntegrationTests", not <| (Environment.hasEnvironVar "skipTests"))
  ==> "StopServer"
  //==> "RunTests"
  //=?> ("GenerateDocs", BuildServer.isLocalBuild)
  ==> "NuGet"
  ==> "All"
  ==> "BuildPackage"
  ==> "PublishNuget"
  ==> "Release"

Target.runOrDefault "BuildPackage"
