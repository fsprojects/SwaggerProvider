module Swashbuckle.v2.ResourceControllersTests

open Expecto
open System
open Swashbuckle.v2.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
    testList "All/v2/Swashbuckle.ResourceControllers.Tests" [

        testCaseAsync "ResourceStringString Add and get from resource dictionary"
        <| async {
            do! api.PutApiResourceStringString("lang2", "F#")
            do! api.GetApiResourceStringString("lang2") |> asyncEqual "F#"
        }

        testCaseAsync "ResourceStringString Update value in the resource dictionary"
        <| async {
            do! api.PutApiResourceStringString("name2", "Sergey")
            do! api.GetApiResourceStringString("name2") |> asyncEqual "Sergey"

            do! api.PostApiResourceStringString("name2", "Siarhei")
            do! api.GetApiResourceStringString("name2") |> asyncEqual "Siarhei"
        }

        testCaseAsync "ResourceStringString Delete from the dictionary"
        <| async {
            let baseUrl = "http://localhost/"
            do! api.PutApiResourceStringString("url", baseUrl)
            let! url = api.GetApiResourceStringString("url")
            shouldEqual url baseUrl
            do! api.DeleteApiResourceStringString("url")
        }
    ]
