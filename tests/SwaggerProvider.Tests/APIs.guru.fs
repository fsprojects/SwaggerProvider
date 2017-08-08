module APIsGuru
open FSharp.Data
open System

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
    |]

let private schemaUrls =
    Array.concat [manualSchemaUrls; apisGuruJsonSchemaUrls]

let private ignoredPrefList =
    [
     // Following schemas require additional investigation and fixes
     "https://api.apis.guru/v2/specs/clarify.io/" // StackOverflowException during FCS compilation
    ]
let private skipIgnored (url:string) =
    ignoredPrefList
    |> List.exists (url.StartsWith)
    |> not

let private rnd = Random(int(DateTime.Now.Ticks))
let private shrinkOnMonoTo size arr =
    if isNull <| Type.GetType ("Mono.Runtime")
    then arr
    else Array.init size (fun _ -> arr.[rnd.Next(size)])

let private filter = Array.filter skipIgnored >> shrinkOnMonoTo 80

let JsonSchemas = filter schemaUrls
let YamlSchemas = filter apisGuruYamlSchemaUrls

