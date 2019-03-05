module APIsGuru

open System
open Newtonsoft.Json.Linq

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
    |> Seq.choose (fun schema ->
        // TODO: choose only latest version to speedup CI
        schema
        |> getProp "versions"
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

let Schemas = 
    lazy (getApisGuruSchemas "swaggerYamlUrl") // "swaggerUrl"
    
