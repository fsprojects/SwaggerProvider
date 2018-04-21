#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net461"

// Standard NuGet locations packages
#I "../Newtonsoft.Json.9.0.1/lib/net461"
#I "../Newtonsoft.Json.10.0.3/lib/net461"
#I "../YamlDotNet.4.3.0/lib/net461"

// Standard Paket locations packages
#I "../Newtonsoft.Json/lib/net461"
#I "../YamlDotNet/lib/net461"

// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference SwaggerProvider and Newtonsoft.Json
#r "Newtonsoft.Json.dll"
#r "YamlDotNet.dll"
#r "SwaggerProvider.Runtime.dll"
#r "SwaggerProvider.dll"
