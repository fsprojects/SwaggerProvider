module SwashbuckleResourceControllersTests

open Expecto
open System
open SwashbuckleReturnControllersTests

[<Tests>]
let resourceControllersTests =
  testList "All/Swashbuckle.ResourceControllers.Tests" [

    testCaseAsync "ResourceStringString Add and get from resource dictionary" <| async {
        do! api.PutApiResourceStringStringAsync("lang", "F#")
        do! api.GetApiResourceStringStringAsync("lang") |> asyncEqual "F#"
    }

    testCaseAsync "ResourceStringString Update value in the resource dictionary" <| async {
        do! api.PutApiResourceStringStringAsync("name", "Sergey")
        do! api.GetApiResourceStringStringAsync("name") |> asyncEqual "Sergey"

        do! api.PostApiResourceStringStringAsync("name", "Siarhei")
        do! api.GetApiResourceStringStringAsync("name") |> asyncEqual "Siarhei"
    }

    testCaseAsync "ResourceStringString Delete from the dictionary" <| async {
        let baseUrl = "http://localhost/"
        do! api.PutApiResourceStringStringAsync("url", baseUrl)
        let! url = api.GetApiResourceStringStringAsync("url")
        shouldEqual url baseUrl
        do! api.DeleteApiResourceStringStringAsync("url")
    }
  ]