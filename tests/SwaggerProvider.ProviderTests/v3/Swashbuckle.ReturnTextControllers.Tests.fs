module Swashbuckle.v3.ReturnTextControllersTests

open Xunit
open FsUnitTyped
open SwaggerProvider
open System
open System.Net.Http

open Swashbuckle.v3.ReturnControllersTests

let asyncEqual expected actualTask =
    task {
        let! actual = actualTask
        actual |> shouldEqual expected
    }

[<Fact>]
let ``Return text/plain GET Test``() =
    api.GetApiReturnPlain() |> asyncEqual "Hello world"

[<Fact>]
let ``Return text/csv GET Test``() =
    api.GetApiReturnCsv() |> asyncEqual "Hello,world"
