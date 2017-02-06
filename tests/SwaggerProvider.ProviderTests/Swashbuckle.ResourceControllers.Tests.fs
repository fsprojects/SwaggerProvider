module SwashbuckleResourceControllersTests

open Expecto
open System
open SwashbuckleReturnControllersTests

let shouldEqual expected actual =
    Expect.equal actual expected "return value"

[<Tests>]
let resourceControllersTests =
  testList "All/Swashbuckle.ResourceControllers.Tests" [

    testCase "ResourceStringString Add and get from resource dictionary" <| fun _ ->
        api.PutApiResourceStringString("lang", "F#")

        api.GetApiResourceStringString("lang")
        |> shouldEqual "F#"

    testCase "ResourceStringString Update value in the resource dictionary" <| fun _ ->
        api.PutApiResourceStringString("name", "Sergey")
        api.GetApiResourceStringString("name")
        |> shouldEqual "Sergey"

        api.PostApiResourceStringString("name", "Siarhei")
        api.GetApiResourceStringString("name")
        |> shouldEqual "Siarhei"

    testCase "ResourceStringString Delete from the dictionary" <| fun _ ->
        api.PutApiResourceStringString("url", "http://localhost/")
        api.DeleteApiResourceStringString("url")
  ]