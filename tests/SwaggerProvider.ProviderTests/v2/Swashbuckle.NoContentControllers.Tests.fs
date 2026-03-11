module Swashbuckle.v2.NoContentControllersTests

open Xunit
open Swashbuckle.v2.ReturnControllersTests

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
