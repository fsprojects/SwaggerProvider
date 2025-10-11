module Swashbuckle.v3.ReturnTextControllersTests

open Xunit
open FsUnitTyped
open SwaggerProvider
open System
open System.Net.Http

open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Return text/plain GET Test``() =
    api.GetApiReturnPlain() |> asyncEqual "Hello world"

[<Fact>]
let ``Return text/csv GET Test``() =
    api.GetApiReturnCsv() |> asyncEqual "Hello,world"
