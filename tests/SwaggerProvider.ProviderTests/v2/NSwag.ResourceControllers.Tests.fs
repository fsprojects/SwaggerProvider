module NSwagResourceControllersTests

open Expecto
open System
open NSwagReturnControllersTests

[<Tests>]
let resourceControllersTests =
  testList "All/Swashbuckle.ResourceControllers.Tests" [

    testCaseAsync "ResourceStringString Add and get from resource dictionary" <| async {
        do! api.PutApiResourceStringString("lang", "F#")
        do! api.GetApiResourceStringString("lang") |> asyncEqual "F#"
    }

    testCaseAsync "ResourceStringString Update value in the resource dictionary" <| async {
        do! api.PutApiResourceStringString("name", "Sergey")
        do! api.GetApiResourceStringString("name") |> asyncEqual "Sergey"

        do! api.PostApiResourceStringString("name", "Siarhei")
        do! api.GetApiResourceStringString("name") |> asyncEqual "Siarhei"
    }

    testCaseAsync "ResourceStringString Delete from the dictionary" <| async {
        let baseUrl = "http://localhost/"
        do! api.PutApiResourceStringString("url", baseUrl)
        let! url = api.GetApiResourceStringString("url")
        shouldEqual url baseUrl
        do! api.DeleteApiResourceStringString("url")
    }
  ]
