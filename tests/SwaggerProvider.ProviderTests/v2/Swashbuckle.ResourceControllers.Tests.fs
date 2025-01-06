module Swashbuckle.v2.ResourceControllersTests

open Xunit
open FsUnitTyped
open Swashbuckle.v2.ReturnControllersTests

[<Fact>]
let ``ResourceStringString Add and get from resource dictionary``() =
    task {
        do! api.PutApiResourceStringString("language", "Fsharp")
        do! api.GetApiResourceStringString("language") |> asyncEqual "Fsharp"
    }

[<Fact>]
let ``ResourceStringString Update value in the resource dictionary``() =
    task {
        do! api.PutApiResourceStringString("name2", "Sergey")
        do! api.GetApiResourceStringString("name2") |> asyncEqual "Sergey"

        do! api.PostApiResourceStringString("name2", "Siarhei")
        do! api.GetApiResourceStringString("name2") |> asyncEqual "Siarhei"
    }

let ``ResourceStringString Delete from the dictionary``() =
    task {
        let baseUrl = "http://localhost/"
        do! api.PutApiResourceStringString("url", baseUrl)
        let! url = api.GetApiResourceStringString("url")
        shouldEqual url baseUrl
        do! api.DeleteApiResourceStringString("url")
    }
