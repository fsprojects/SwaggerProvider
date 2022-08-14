#I "/Users/sergey/github/SwaggerProvider/src/SwaggerProvider.Runtime/bin/Release/netstandard2.0"
#I "/Users/sergey/github/SwaggerProvider/src/SwaggerProvider.Runtime/bin/Release/typeproviders/fsharp41/netstandard2.0"
#r "SwaggerProvider.Runtime.dll"
#r "SwaggerProvider.DesignTime.dll"

open SwaggerProvider

[<Literal>]
let Schema = "http://localhost:5000/swagger/v1/swagger.json"

type TP = SwaggerClientProvider<Schema>

//let client = TP.Client()
