// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Web site location for the generated documentation
let website = "/SwaggerProvider"

let githubLink = "https://github.com/fsprojects/SwaggerProvider"

// Specify more information about your project
let info =
  [ "project-name", "SwaggerProvider"
    "project-author", "Sergey Tihon"
    "project-summary", "Type provider for Swagger.io"
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/SwaggerProvider" 
  ]

// --------------------------------------------------------------------------------------
// For a typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/build/FAKE/tools/"
#load "../../packages/build/FSharp.Formatting/FSharp.Formatting.fsx"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output")
#endif

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/build/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

let references =
  if isMono then
    // Workaround compiler errors in Razor-ViewEngine
    let d = RazorEngine.Compilation.ReferenceResolver.UseCurrentAssembliesReferenceResolver()
    let loadedList = d.GetReferences () |> Seq.map (fun r -> r.GetFile()) |> Seq.cache
    // We replace the list and add required items manually as mcs doesn't like duplicates...
    let getItem name = loadedList |> Seq.find (fun l -> l.Contains name)
    [ (getItem "FSharp.Core").Replace("4.3.0.0", "4.3.1.0")
      Path.GetFullPath "./../../packages/build/FSharp.Compiler.Service/lib/net40/FSharp.Compiler.Service.dll"
      Path.GetFullPath "./../../packages/build/FSharp.Formatting/lib/net40/System.Web.Razor.dll"
      Path.GetFullPath "./../../packages/build/FSharp.Formatting/lib/net40/RazorEngine.dll"
      Path.GetFullPath "./../../packages/build/FSharp.Formatting/lib/net40/FSharp.Literate.dll"
      Path.GetFullPath "./../../packages/build/FSharp.Formatting/lib/net40/FSharp.CodeFormat.dll"
      Path.GetFullPath "./../../packages/build/FSharp.Formatting/lib/net40/FSharp.MetadataFormat.dll" ]
    |> Some
  else None

let binaries =
    directoryInfo bin 
    |> subDirectories
    |> Array.map (fun d -> d.FullName @@ (sprintf "%s.dll" d.Name))
    |> List.ofArray

let libDirs =
    directoryInfo bin 
    |> subDirectories
    |> Array.map (fun d -> d.FullName)
    |> List.ofArray

// Build API reference from XML comments
let buildReference () =
  CleanDir (output @@ "reference")
  MetadataFormat.Generate
    ( binaries, 
      output @@ "reference", 
      layoutRootsAll.["en"],
      parameters = ("root", root)::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      ?assemblyReferences = references,
      publicOnly = true,
      libDirs = libDirs 
    )

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    let langSpecificPath(lang, path:string) =
        path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.exists(fun i -> i = lang)
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    Literate.ProcessDirectory
      ( dir, docTemplate, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        ?assemblyReferences = references,
        generateAnchors = true )

// Generate
copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
