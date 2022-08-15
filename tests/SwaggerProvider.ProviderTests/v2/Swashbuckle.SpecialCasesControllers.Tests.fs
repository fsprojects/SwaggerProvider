module Swashbuckle.SpecialCasesControllersTests

open Expecto
open System
open Swashbuckle.v2.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
    testList "All/v2/Swashbuckle.SpecialCasesControllers.Tests" [

        testCaseAsync "Requst response in JSON format from MultiFormatController"
        <| async { do! api.GetApiMultiFormat() |> asyncEqual "0.0" }
    ]
