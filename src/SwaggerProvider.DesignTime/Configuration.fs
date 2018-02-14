/// [omit]
module SwaggerProvider.Internal.Configuration

open System
open System.IO
open System.Reflection
open System.Configuration
open System.Collections.Generic

type Logging() =
  static member logf (s: string) =
    () // File.AppendAllLines("swaggerlog", [|s|])

/// Returns the Assembly object of SwaggerProvider.Runtime.dll (this needs to
/// work when called from SwaggerProvider.DesignTime.dll)
let swaggerRuntimeAssy =
  AppDomain.CurrentDomain.GetAssemblies()
  |> Seq.find (fun a -> a.FullName.StartsWith("SwaggerProvider,"))

/// Finds directories relative to 'dirs' using the specified 'patterns'.
/// Patterns is a string, such as "..\foo\*\bar" split by '\'. Standard
/// .NET libraries do not support "*", so we have to do it ourselves..
let rec searchDirectories (patterns:string list) dirs =
  match patterns with
  | [] -> dirs
  | name::patterns when name.EndsWith("*") ->
    let prefix = name.TrimEnd([|'*'|])
    dirs
    |> List.collect (fun dir ->
      Directory.GetDirectories dir
      |> Array.filter (fun x -> x.IndexOf(prefix, dir.Length) >= 0)
      |> List.ofArray
    )
    |> searchDirectories patterns
  | name::patterns ->
    dirs
    |> List.map (fun d -> Path.Combine(d, name))
    |> searchDirectories patterns

/// Returns the real assembly location - when shadow copying is enabled, this
/// returns the original assembly location (which may contain other files we need)
let getAssemblyLocation (assem:Assembly) =
  if System.AppDomain.CurrentDomain.ShadowCopyFiles
  then (System.Uri(assem.EscapedCodeBase)).LocalPath
  else assem.Location

/// Reads the 'SwaggerProvider.dll.config' file and gets the 'ProbingLocations'
/// parameter from the configuration file. Resolves the directories and returns
/// them as a list.
let probingLocations =
  try
    let rootExe = getAssemblyLocation swaggerRuntimeAssy
    let rootDir = Path.GetDirectoryName rootExe
    Logging.logf <| sprintf "Root %s" rootDir
    let config = System.Configuration.ConfigurationManager.OpenExeConfiguration(rootExe)
    let pattern = config.AppSettings.Settings.["ProbingLocations"]
    if isNull pattern
    then []
    else
      Logging.logf <| sprintf "Probing patterns %A" (pattern.Value.Split(';'))
      let dirs =
        [ yield rootDir
          let pattern = pattern.Value.Split(';', ',') |> List.ofSeq
          for pat in pattern do
            let roots = [ rootDir ]
            for dir in roots |> searchDirectories (List.ofSeq (pat.Split('/','\\'))) do
              if Directory.Exists(dir)
              then yield Path.GetFullPath(dir) ]
      Logging.logf (sprintf "Found probing directories: %A" dirs)
      dirs
  with :? ConfigurationErrorsException | :? KeyNotFoundException -> []

/// Given an assembly name, try to find it in either assemblies
/// loaded in the current AppDomain, or in one of the specified
/// probing directories.
let resolveReferencedAssembly (asmName:string) =

  // Do not interfere with loading FSharp.Core resources, see #97
  if asmName.StartsWith "FSharp.Core.resources" then null else

  // First, try to find the assembly in the currently loaded assemblies
  let fullName = AssemblyName(asmName)
  let loadedAsm =
    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Seq.tryFind (fun a -> AssemblyName.ReferenceMatchesDefinition(fullName, a.GetName()))
  match loadedAsm with
  | Some asm ->
    Logging.logf (sprintf "found assembly %s" asm.FullName)
    asm
  | None ->
    // Otherwise, search the probing locations for a DLL file
    let libraryName =
      let idx = asmName.IndexOf(',')
      if idx > 0 then asmName.Substring(0, idx) else asmName

    let asm = probingLocations |> Seq.tryPick (fun dir ->
      let library = Path.Combine(dir, libraryName+".dll")
      if File.Exists(library) then
        Logging.logf <| sprintf "Found assembly, checking version! (%s)" library
        // We do a ReflectionOnlyLoad so that we can check the version
        let refAssem = Assembly.ReflectionOnlyLoadFrom(library)
        // If it matches, we load the actual assembly
        if refAssem.FullName = asmName then
          Logging.logf "...version matches, returning!"
          Some(Assembly.LoadFrom(library))
        else
          Logging.logf "...version mismatch, skipping"
          None
      else
        Logging.logf <| sprintf "Didn't find library %s" libraryName
        None)

    if asm = None then Logging.logf <| sprintf "Assembly not found! %s" asmName
    defaultArg asm null
