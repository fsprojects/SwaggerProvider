module Swashbuckle.v3.FileControllersTests

open Expecto
open System
open System.IO
open Swashbuckle.v3.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
  testList "All/v3/Swashbuckle.FileControllersTests.Tests" [

    testCaseAsync "Download file as IO.Stream" <| async {
        let! stream = api.GetApiReturnFile()
        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync() |> Async.AwaitTask
        Expect.stringContains text "I am totally a file" "incorrect server response"
    }

    testCaseAsync "Send file and get it back" <| async {
        let text = "This is test file"
        let bytes = System.Text.Encoding.UTF8.GetBytes(text)
        let stream = new MemoryStream(bytes)

        let data = WebAPI.OperationTypes.PostApiReturnFileSingle_formData(stream)
        let! stream = api.PostApiReturnFileSingle(data)
        use reader = new StreamReader(stream)
        let! outText = reader.ReadToEndAsync() |> Async.AwaitTask
        Expect.equal outText text "incorrect server response"
    }

    testCaseAsync "Send form-with-file and get it back as IO.Stream" <| async {
        let text = "This is test file"
        let bytes = System.Text.Encoding.UTF8.GetBytes(text)
        let stream = new MemoryStream(bytes)

        let data = WebAPI.OperationTypes.PostApiReturnFileFormWithFile_formData("newName.txt", stream)
        let! stream = api.PostApiReturnFileFormWithFile(data)
        use reader = new StreamReader(stream)
        let! outText = reader.ReadToEndAsync() |> Async.AwaitTask
        Expect.equal outText text "incorrect server response"
    }

  ]
