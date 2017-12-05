module APIsGuru

open System
open Newtonsoft.Json.Linq

// Test that provider can parse real-word Swagger 2.0 schemes
// https://github.com/APIs-guru/api-models/blob/master/API.md

let private apisGuruList = lazy (
    printfn "Loading APIs.Guru list ..."
    use client = new Net.WebClient()
    let list = client.DownloadString("https://api.apis.guru/v2/list.json")
    JObject.Parse(list).Properties()
    |> Seq.map (fun x->x.Value)
  )

let private getApisGuruSchemas propertyName =
    let getProp prop (obj:JToken) =
        match obj  with
        | :? JObject as jObj ->
            match jObj.TryGetValue(prop) with
            | true, jToken -> Some jToken
            | _ -> None
        | _ -> None
    apisGuruList.Value
    |> Seq.choose (fun x ->
        x |> getProp "versions"
        |> Option.bind (fun v ->
            let jObj = v :?> JObject
            jObj.Properties()
            |> Seq.choose (fun y -> y.Value |> getProp propertyName)
            |> Some)
       )
    |> Seq.concat
    |> Seq.map (fun x->x.ToObject<string>())
    |> Seq.toArray

let private apisGuruJsonSchemaUrls = getApisGuruSchemas "swaggerUrl"
let private apisGuruYamlSchemaUrls = getApisGuruSchemas "swaggerYamlUrl"

let private manualSchemaUrls =
    [|//"https://www.expedia.com/static/mobile/swaggerui/swagger.json" // This schema is incorrect
      "https://eaccountingapi-sandbox.test.vismaonline.com/swagger/docs/v2"
    |]

let schemaUrls =
    Array.concat [manualSchemaUrls; apisGuruJsonSchemaUrls]

let private ignoredPrefList =
    [
     // Following schemas require additional investigation and fixes
    ]
let private skipIgnored (url:string) =
    ignoredPrefList
    |> List.exists (url.StartsWith)
    |> not

let private rnd = Random(int(DateTime.Now.Ticks))
let shrink size (arr:'a[]) = 
    Array.init size (fun _ -> arr.[rnd.Next(size)])
let private shrinkOnMonoTo size arr =
    if isNull <| Type.GetType ("Mono.Runtime")
    then arr else arr |> shrink size

let private filter = Array.filter skipIgnored >> shrinkOnMonoTo 80

let JsonSchemas = filter schemaUrls
let YamlSchemas = filter apisGuruYamlSchemaUrls

