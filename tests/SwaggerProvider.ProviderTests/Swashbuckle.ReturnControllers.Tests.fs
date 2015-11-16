module Swashbuckle

open NUnit.Framework
open FsUnit
open SwaggerProvider

type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1">

[<Test>]
let ``Return String GET Test`` () =
    WebAPI.ReturnString.Get()
    |> should equal "Hello world"

[<Test>]
let ``Return String POST Test`` () =
    WebAPI.ReturnString.Post()
    |> should equal "Hello world"
