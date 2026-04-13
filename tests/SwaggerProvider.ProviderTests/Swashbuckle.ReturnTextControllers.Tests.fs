module Swashbuckle.ReturnTextControllersTests

open Xunit
open FsUnitTyped
open SwaggerProvider
open System
open System.Net.Http

open Swashbuckle.ReturnControllersTests

[<Fact>]
let ``Return text/plain GET Test``() =
    api.GetApiReturnPlain() |> asyncEqual "Hello world"

[<Fact>]
let ``Return text/csv GET Test``() =
    api.GetApiReturnCsv() |> asyncEqual "Hello,world"

[<Fact>]
let ``Send & return text/plain POST Test``() =
    api.PostApiConsumesText("hello") |> asyncEqual "hello"

// Test for https://github.com/fsprojects/SwaggerProvider/pull/290 to check for expected 'Accept' header values
[<Fact>]
let ``Return text/plain Accept header Test``() =
    api.GetApiCheckAcceptsPlain() |> asyncEqual "Hello world"

[<Fact>]
let ``Return text/csv Accept header Test``() =
    api.GetApiCheckAcceptsCsv() |> asyncEqual "Hello,world"
