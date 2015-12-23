#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net45"

// Standard NuGet locations packages
#I "../Newtonsoft.Json.7.0.1/lib/net40"
#I "../YamlDotNet.3.7.0/lib/net35"

// Standard Paket locations packages
#I "../Newtonsoft.Json/lib/net40"
#I "../YamlDotNet/lib/net35"

// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference SwaggerProvider and Newtonsoft.Json
#r "Newtonsoft.Json.dll"
#r "YamlDotNet.dll"
#r "SwaggerProvider.dll"
#r "SwaggerProvider.Runtime.dll"
