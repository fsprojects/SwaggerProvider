#I "../src/SwaggerProvider.Runtime/bin/Release/netstandard2.0"
#I "../src/SwaggerProvider.Runtime/bin/Release/typeproviders/fsharp41/netstandard2.0"
#r "SwaggerProvider.Runtime.dll"
#r "SwaggerProvider.DesignTime.dll"

open SwaggerProvider

[<Literal>]
let Schema = "https://petstore.swagger.io/v2/swagger.json"

type TP = SwaggerClientProvider<Schema>

//let client = TP.Client()
