module APIsGuru
open FSharp.Data
open System.IO

// Test that provider can parse real-word Swagger 2.0 schemes
// https://github.com/APIs-guru/api-models/blob/master/API.md

let [<Literal>] private SampleJson = __SOURCE_DIRECTORY__ + "/APIs.guru.list.sample.json"
type ApisGuru = FSharp.Data.JsonProvider<SampleJson>

let private apisGuruList = lazy (
    ApisGuru
        .Load("https://api.apis.guru/v2/list.json")
        .JsonValue.Properties()
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
    |> Array.map (fun x->x.AsString())

let private apisGuruJsonSchemaUrls = getApisGuruSchemas "swaggerUrl"
let private apisGuruYamlSchemaUrls = getApisGuruSchemas "swaggerYamlUrl"

let private manualSchemaUrls =
    [|"http://netflix.github.io/genie/docs/rest/swagger.json"
      //"https://www.expedia.com/static/mobile/swaggerui/swagger.json" // This schema is incorrect
      "https://graphhopper.com/api/1/vrp/swagger.json"|]

let private schemaUrls =
    Array.concat [manualSchemaUrls; apisGuruJsonSchemaUrls]

let private ignoreList =
    ["https://api.apis.guru/v2/specs/sendgrid.com/3.0/swagger.json"
     "https://api.apis.guru/v2/specs/sendgrid.com/3.0/swagger.yaml"
    ] |> Set.ofList
let private skipIgnored = ignoreList.Contains >> not

let JsonSchemas = Array.filter skipIgnored schemaUrls
let YamlSchemas = Array.filter skipIgnored apisGuruYamlSchemaUrls
