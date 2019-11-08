module Swashbuckle.v3.FileControllersTests

open Expecto
open System
open System.IO
open Swashbuckle.v3.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
  testList "All/v3/Swashbuckle.FileControllersTests.Tests" [

    testCaseAsync "Download file as IStream" <| async {
        let! stream = api.GetApiReturnFile()
        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync() |> Async.AwaitTask
        Expect.stringContains text "I am totally a file" "incorrect server response"
    }

  ]
