module Swashbuckle.NoContentControllersTests

open FsUnitTyped
open Xunit
open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Test 204 with GET``() =
    task { do! api.GetApiNoContent() }

[<Fact>]
let ``Test 204 with POST``() =
    task { do! api.PostApiNoContent() }

[<Fact>]
let ``Test 204 with PUT``() =
    task { do! api.PutApiNoContent() }

[<Fact>]
let ``Test 204 with DELETE``() =
    task { do! api.DeleteApiNoContent() }

[<Fact>]
let ``Test 202 Accepted with GET returns string``() =
    task {
        let! result = api.GetApiAccepted()
        result |> shouldEqual "accepted-value"
    }

[<Fact>]
let ``Test 202 Accepted with POST returns string``() =
    task {
        let! result = api.PostApiAccepted()
        result |> shouldEqual "accepted-value"
    }
