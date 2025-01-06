module Swashbuckle.v3.NoContentControllersTests

open Xunit
open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Test 204 with GET``() =
    task { do! api.GetNoContent() }

[<Fact>]
let ``Test 204 with POST``() =
    task { do! api.PostNoContent() }

[<Fact>]
let ``Test 204 with PUT``() =
    task { do! api.PutNoContent() }

[<Fact>]
let ``Test 204 with DELETE``() =
    task { do! api.DeleteNoContent() }
