namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("SwaggerProvider")>]
[<assembly: AssemblyProductAttribute("SwaggerProvider")>]
[<assembly: AssemblyDescriptionAttribute("F# Type Provider for Swagger")>]
[<assembly: AssemblyVersionAttribute("0.5.4")>]
[<assembly: AssemblyFileVersionAttribute("0.5.4")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.5.4"
    let [<Literal>] InformationalVersion = "0.5.4"
