module APIsGuru

open System.Text.Json
open System.Net.Http

let httpClient = new HttpClient()

let private apisGuruList =
    lazy
        (printfn "Loading APIs.Guru list ..."

         let list =
             httpClient.GetStringAsync("https://api.apis.guru/v2/list.json")
             |> Async.AwaitTask
             |> Async.RunSynchronously

         JsonDocument.Parse(list).RootElement.EnumerateObject()
         |> Seq.map(fun x -> x.Value))

let private getApisGuruSchemas propertyName =
    let getProp (prop: string) (obj: JsonElement) =
        match obj.ValueKind with
        | JsonValueKind.Object ->
            match obj.TryGetProperty(prop) with
            | true, jToken -> Some jToken
            | _ -> None
        | _ -> None

    apisGuruList.Value
    |> Seq.choose(fun schema ->
        schema
        |> getProp "versions"
        |> Option.bind(fun v -> v.EnumerateObject() |> Seq.map(fun y -> y.Value) |> Seq.last |> Some))
    |> Seq.choose(getProp propertyName)
    |> Seq.map(fun x -> x.GetString())
    |> Seq.toArray

let Schemas = lazy (getApisGuruSchemas "swaggerYamlUrl") // "swaggerUrl"
