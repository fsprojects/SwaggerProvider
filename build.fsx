// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

// Added to allow building the script from F# interactive. If the build fails F#
// interactive allows you to review the full log, unlike the Windows Command Prompt.
#if INTERACTIVE
System.IO.Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)
#endif

#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.Testing.Expecto
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "F# Type Provider for Swagger"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies =
   !! "tests/**/bin/Release/net461/*Tests*.exe"
     -- "tests/*.CSharp/bin/Release/net461/*.exe"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
// The name of the project on GitHub
let gitName = "SwaggerProvider"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let fileName = "src/Common/AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title gitName
        Attribute.Product gitName
        Attribute.Description description
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ]
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp"]
    try System.IO.File.Delete("swaggerlog") with | _ -> ()
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

let mutable dotnetExePath = "dotnet"
let dotnetcliVersion = "2.1.401"

Target "InstallDotNetCore" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
    Environment.SetEnvironmentVariable("DOTNET_EXE_PATH", dotnetExePath)
)

Target "Build" (fun _ ->
    DotNetCli.Build (fun c ->
        { c with
            Project = "SwaggerProvider.sln"
            Configuration = "Release"
            ToolPath = dotnetExePath })
)

Target "StartServer" (fun _ ->
    ProcessHelper.StartProcess (fun prInfo ->
        prInfo.FileName <- "tests/Swashbuckle.OWIN.Server/bin/Release/net461/Swashbuckle.OWIN.Server.exe")
    System.Threading.Thread.Sleep(2000)
)

Target "BuildTests" (fun _ ->
    DotNetCli.Build (fun c ->
        { c with
            Project = "SwaggerProvider.TestsAndDocs.sln"
            Configuration = "Release"
            ToolPath = dotnetExePath })
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunUnitTests" (fun _ ->
    testAssemblies
    |> Expecto (fun p ->
        { p with
            Filter = "All/" } )
    |> ignore
)

Target "RunIntegrationTests" (fun _ ->
    testAssemblies
    |> Expecto (fun p ->
        { p with
            Filter = "Integration/" } )
    |> ignore
)

FinalTarget "StopServer" (fun _ ->
    ProcessHelper.killProcess "Swashbuckle.OWIN.Server"
)

Target "RunTests" DoNothing

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    Paket.Pack(fun p ->
        { p with
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes})
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            WorkingDir = "bin" })
)


// --------------------------------------------------------------------------------------
// Generate the documentation

module Fake =
    let fakePath = "packages" </> "build" </> "FAKE" </> "tools" </> "FAKE.exe"
    let fakeStartInfo script workingDirectory args fsiargs environmentVars =
        (fun (info: Diagnostics.ProcessStartInfo) ->
            info.FileName <- Path.GetFullPath fakePath
            info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
            info.WorkingDirectory <- workingDirectory
            let setVar k v = info.EnvironmentVariables.[k] <- v
            for (k, v) in environmentVars do setVar k v
            setVar "MSBuild" msBuildExe
            setVar "GIT" CommandHelper.gitPath
            setVar "FSI" fsiPath)

    /// Run the given buildscript with FAKE.exe
    let executeFAKEWithOutput workingDirectory script fsiargs envArgs =
        let exitCode =
            ExecProcessWithLambdas
                (fakeStartInfo script workingDirectory "" fsiargs envArgs)
                TimeSpan.MaxValue false ignore ignore
        System.Threading.Thread.Sleep 1000
        exitCode

Target "BrowseDocs" (fun _ ->
    let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "BrowseDocs"]
    if exit <> 0 then failwith "Browsing documentation failed"
)

Target "GenerateDocs" (fun _ ->
    let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "GenerateDocs"]
    if exit <> 0 then failwith "Generating documentation failed"
)

Target "PublishDocs" (fun _ ->
    let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "PublishDocs"]
    if exit <> 0 then failwith "Publishing documentation failed"
)

Target "PublishStaticPages" (fun _ ->
    let exit = Fake.executeFAKEWithOutput "docs" "docs.fsx" "" ["target", "PublishStaticPages"]
    if exit <> 0 then failwith "Publishing documentation failed"
)

// --------------------------------------------------------------------------------------
// Release Scripts

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion

    // release on github
    createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // TODO: |> uploadFile "PATH_TO_FILE"
    |> releaseDraft
    |> Async.RunSynchronously
)

Target "BuildPackage" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "InstallDotNetCore"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "StartServer"
  ==> "BuildTests"
  ==> "RunUnitTests"
  =?> ("RunIntegrationTests", not <| (hasBuildParam "skipTests"))
  ==> "StopServer"
  ==> "RunTests"
  =?> ("GenerateDocs",isLocalBuild)
  ==> "All"
  ==> "NuGet"
  ==> "BuildPackage"
  ==> "PublishNuget"
  ==> "Release"

RunTargetOrDefault "BuildPackage"
