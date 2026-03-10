module Swashbuckle.v3.SpecialCasesControllersTests

open Xunit
open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Request response in JSON format from MultiFormatController``() =
    task { do! api.GetApiMultiFormat() |> asyncEqual "0.0" }

// Regression test for https://github.com/fsprojects/SwaggerProvider/issues/141
// Path parameter values containing $ (e.g. "$0something") must be passed literally.
[<Fact>]
let ``Path parameter containing dollar sign is not treated as regex back-reference``() =
    task { do! api.GetApiEchoPath("$0something") |> asyncEqual "$0something" }
