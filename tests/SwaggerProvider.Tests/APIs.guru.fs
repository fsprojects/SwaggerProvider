module APIsGuru

open Newtonsoft.Json.Linq
open System.Net.Http

let httpClient = new HttpClient()

let private apisGuruList = lazy (
    printfn "Loading APIs.Guru list ..."
    let list =
        httpClient.GetStringAsync("https://api.apis.guru/v2/list.json")
        |> Async.AwaitTask
        |> Async.RunSynchronously 
    JObject.Parse(list).Properties()
    |> Seq.map (fun x -> x.Value)
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
        schema
        |> getProp "versions"
        |> Option.bind (fun v ->
            let jObj = v :?> JObject
            jObj.Properties()
            |> Seq.map (fun y -> y.Value)
            |> Seq.last
            |> Some)
       )
    |> Seq.choose (getProp propertyName)
    |> Seq.map (fun x -> x.ToObject<string>())
    |> Seq.toArray

let Schemas = 
    lazy (getApisGuruSchemas "swaggerYamlUrl") // "swaggerUrl"
    
