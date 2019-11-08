module Swashbuckle.v3.FileControllersTests

open Expecto
open System
open Swashbuckle.v3.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
  testList "All/v3/Swashbuckle.FileControllersTests.Tests" [

    testCaseAsync "Download file as IStream" <| async {
        do! api.GetApiReturnFile()
    }

  ]
