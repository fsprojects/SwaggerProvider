module Swashbuckle.CustomEnumControllersTests

open Xunit
open Swashbuckle.ReturnControllersTests

[<Fact>]
let ``Return Priority GET Test``() =
    api.GetApiReturnPriority() |> asyncEqual WebAPI.Priority.High

[<Fact>]
let ``Return Priority POST Test``() =
    api.PostApiReturnPriority() |> asyncEqual WebAPI.Priority.High

[<Fact>]
let ``Return Array Priority GET Test``() =
    api.GetApiReturnArrayPriority()
    |> asyncEqual
        [| WebAPI.Priority.Low
           WebAPI.Priority.Normal
           WebAPI.Priority.High
           WebAPI.Priority.Critical |]

[<Fact>]
let ``Return Array Priority POST Test``() =
    api.PostApiReturnArrayPriority()
    |> asyncEqual
        [| WebAPI.Priority.Low
           WebAPI.Priority.Normal
           WebAPI.Priority.High
           WebAPI.Priority.Critical |]

[<Fact>]
let ``Update Priority GET Test``() =
    api.GetApiUpdatePriority(Some WebAPI.Priority.Critical)
    |> asyncEqual WebAPI.Priority.Critical

[<Fact>]
let ``Update Priority POST Test``() =
    api.PostApiUpdatePriority(Some WebAPI.Priority.Critical)
    |> asyncEqual WebAPI.Priority.Critical

[<Fact>]
let ``Update Array Priority GET Test``() =
    api.GetApiUpdateArrayPriority(
        [| WebAPI.Priority.Critical
           WebAPI.Priority.High
           WebAPI.Priority.Normal
           WebAPI.Priority.Low |]
    )
    |> asyncEqual
        [| WebAPI.Priority.Low
           WebAPI.Priority.Normal
           WebAPI.Priority.High
           WebAPI.Priority.Critical |]

[<Fact>]
let ``Update Array Priority POST Test``() =
    api.PostApiUpdateArrayPriority(
        [| WebAPI.Priority.Critical
           WebAPI.Priority.High
           WebAPI.Priority.Normal
           WebAPI.Priority.Low |]
    )
    |> asyncEqual
        [| WebAPI.Priority.Low
           WebAPI.Priority.Normal
           WebAPI.Priority.High
           WebAPI.Priority.Critical |]
