module APIsGuru
open FSharp.Data

// Test that provider can parse real-word Swagger 2.0 schemes
// https://github.com/APIs-guru/api-models/blob/master/API.md

let private apisGuruList = lazy (
    printfn "Loading APIs.Guru list ..."
    use client = new System.Net.WebClient()
    let list = client.DownloadString("https://api.apis.guru/v2/list.json")
    JsonValue.Parse(list)
             .Properties()
  )

let private getApisGuruSchemas propertyName =
    apisGuruList.Value
    |> Array.choose (fun (name, obj)->
        obj.TryGetProperty("versions")
        |> Option.bind (fun v->
            v.Properties()
            |> Array.choose (fun (_,x)-> x.TryGetProperty(propertyName))
            |> Some)
       )
    |> Array.concat
    |> Array.map (fun x->
        FSharp.Data.JsonExtensions.AsString(x))

let private apisGuruJsonSchemaUrls = getApisGuruSchemas "swaggerUrl"
let private apisGuruYamlSchemaUrls = getApisGuruSchemas "swaggerYamlUrl"

let private manualSchemaUrls =
    [|//"https://www.expedia.com/static/mobile/swaggerui/swagger.json" // This schema is incorrect
      "https://graphhopper.com/api/1/vrp/swagger.json"|]

let private schemaUrls =
    Array.concat [manualSchemaUrls; apisGuruJsonSchemaUrls]

let private ignoreList =
    [
     // Following schemas require additional investigation and fixes
     "https://api.apis.guru/v2/specs/clarify.io/1.3.4/swagger.json" // StackOverflowException during FCS compilation
     "https://api.apis.guru/v2/specs/clarify.io/1.3.4/swagger.yaml"
    ] |> Set.ofList
let private skipIgnored = ignoreList.Contains >> not

let JsonSchemas = Array.filter skipIgnored schemaUrls
let YamlSchemas = Array.filter skipIgnored apisGuruYamlSchemaUrls
