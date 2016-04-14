module SwashbuckleResourceControllersTests

open NUnit.Framework
open FsUnitTyped
open System
open SwashbuckleReturnControllersTests

[<Test>]
let ``ResourceStringString Add and get from resource dictionary`` () =
    api.PutApiResourceStringString("lang", "F#")

    api.GetApiResourceStringString("lang")
    |> shouldEqual "F#"

[<Test>]
let ``ResourceStringString Update value in the resource dictionary`` () =
    api.PutApiResourceStringString("name", "Sergey")
    api.GetApiResourceStringString("name")
    |> shouldEqual "Sergey"

    api.PostApiResourceStringString("name", "Siarhei")
    api.GetApiResourceStringString("name")
    |> shouldEqual "Siarhei"

[<Test>]
let ``ResourceStringString Delete from the dictionary`` () =
    api.PutApiResourceStringString("url", "http://localhost/")
    api.DeleteApiResourceStringString("url")
