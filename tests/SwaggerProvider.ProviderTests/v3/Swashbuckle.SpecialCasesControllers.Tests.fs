module Swashbuckle.v3.SpecialCasesControllersTests

open Expecto
open System
open Swashbuckle.v3.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
    testList "All/v3/Swashbuckle.SpecialCasesControllers.Tests" [

        testCaseAsync "Requst response in JSON format from MultiFormatController"
        <| async { do! api.GetApiMultiFormat() |> asyncEqual "0.0" }
    ]
