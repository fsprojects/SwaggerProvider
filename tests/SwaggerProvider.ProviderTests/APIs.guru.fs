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
        // TODO: choose only latest version to speedup CI
        x |> getProp "versions"
        |> Option.bind (fun v ->
            let jObj = v :?> JObject
            jObj.Properties()
            |> Seq.map (fun y -> y.Value)
            |> Some)
       )
    |> Seq.concat
    |> Seq.choose (fun x ->
        let version =
          x |> getProp "info"
          |> Option.bind (fun y -> y |> getProp "x-origin")
          |> Option.map (fun y -> (y :?> JArray) |> Seq.head)
          |> Option.bind (fun y -> y |> getProp "version")
          |> Option.map (fun y -> y.ToObject<string>())
        match version with
        // TODO: support OpenAPI 3.0 schemas when OpenApi.NET will be ready
        | Some("2.0") -> x |> getProp propertyName
        | _ -> None)
    |> Seq.map (fun x -> x.ToObject<string>())
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

    // System.Exception: Reference to unknown type #/definitions/CalendarEventResource/properties/attributes
    // at Swagger.Parser.Parsers.|IsComposition|_|@209(Dictionary`2 definitions, SchemaNode obj) in C:\projects\swaggerprovider\src\SwaggerProvider.Runtime\Parser\Parsers.fs:line 214
    "https://api.apis.guru/v2/specs/twinehealth.com/"
    ]
let private skipIgnored (url:string) =
    ignoredPrefList
    |> List.exists (url.StartsWith)
    |> not

let private rnd = Random(int(DateTime.Now.Ticks))
let shrink size (arr:'a[]) =
    Array.init size (fun _ -> arr.[rnd.Next(arr.Length)])

let private shrinkOnCI arr =
    if not <| isNull (Environment.GetEnvironmentVariable "TRAVIS")
    then arr |> shrink 80
    elif not <| isNull (Environment.GetEnvironmentVariable "APPVEYOR")
    then arr |> shrink 200
    else arr

let private filter = Array.filter skipIgnored >> shrinkOnCI

let JsonSchemas =
    filter schemaUrls
    |> Array.distinct
let YamlSchemas =
    filter apisGuruYamlSchemaUrls
    |> Array.distinct

