module Swashbuckle.v2.ResourceControllersTests

open Xunit
open FsUnitTyped
open Swashbuckle.v2.ReturnControllersTests

[<Fact>]
let ``ResourceStringString Add and get from resource dictionary``() =
    task {
        do! api.PutApiResourceStringString("lang", "F#")
        do! api.GetApiResourceStringString("lang") |> asyncEqual "F#"
    }

[<Fact>]
let ``ResourceStringString Update value in the resource dictionary``() =
    task {
        do! api.PutApiResourceStringString("name", "Sergey")
        do! api.GetApiResourceStringString("name") |> asyncEqual "Sergey"

        do! api.PostApiResourceStringString("name", "Siarhei")
        do! api.GetApiResourceStringString("name") |> asyncEqual "Siarhei"
    }

let ``ResourceStringString Delete from the dictionary``() =
    task {
        let baseUrl = "http://localhost/"
        do! api.PutApiResourceStringString("url", baseUrl)
        let! url = api.GetApiResourceStringString("url")
        shouldEqual url baseUrl
        do! api.DeleteApiResourceStringString("url")
    }
