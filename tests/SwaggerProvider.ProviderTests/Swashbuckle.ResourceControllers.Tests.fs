module SwashbuckleResourceControllersTests

open NUnit.Framework
open FsUnit
open System
open SwashbuckleReturnControllersTests

type Dict = WebAPI.ResourceStringString

[<Test>]
let ``ResourceStringString Add and get from resource dictionary`` () =
    Dict.Put("lang", "F#")

    Dict.Get("lang")
    |> should equal "F#"

[<Test>]
let ``ResourceStringString Update value in the resource dictionary`` () =
    Dict.Put("name", "Sergey")
    Dict.Get("name")
    |> should equal "Sergey"

    Dict.Post("name", "Siarhei")
    Dict.Get("name")
    |> should equal "Siarhei"

[<Test>]
let ``ResourceStringString Delete from the dictionary`` () =
    Dict.Put("url", "http://localhost/")
    Dict.Delete("url")
