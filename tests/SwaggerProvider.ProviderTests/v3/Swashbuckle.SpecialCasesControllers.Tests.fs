module Swashbuckle.v3.SpecialCasesControllersTests

open Xunit
open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Request response in JSON format from MultiFormatController``() =
    task { do! api.GetApiMultiFormat() |> asyncEqual "0.0" }
