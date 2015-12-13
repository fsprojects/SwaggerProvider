#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net40"

// Standard NuGet locations for Newtonsoft.Json
#I "../Newtonsoft.Json.7.0.1/lib/net40"

// Standard Paket locations for Newtonsoft.Json
#I "../Newtonsoft.Json/lib/net40"

// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference SwaggerProvider and Newtonsoft.Json
#r "Newtonsoft.Json.dll"
#r "SwaggerProvider.dll"
#r "SwaggerProvider.Runtime.dll"
