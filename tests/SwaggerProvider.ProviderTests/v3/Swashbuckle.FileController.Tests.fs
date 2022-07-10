module Swashbuckle.v3.FileControllersTests

open Expecto
open System
open System.IO
open Swashbuckle.v3.ReturnControllersTests

[<Tests>]
let resourceControllersTests =
    let text = "This is test file content"

    let toStream(text: string) =
        let bytes = System.Text.Encoding.UTF8.GetBytes(text)
        new MemoryStream(bytes)

    let fromStream(stream: IO.Stream) = async {
        use reader = new StreamReader(stream)
        return! reader.ReadToEndAsync() |> Async.AwaitTask
    }

    testList "All/v3/Swashbuckle.FileControllersTests.Tests" [

        testCaseAsync "Download file as IO.Stream"
        <| async {
            let! stream = api.GetApiReturnFile()
            let! actual = fromStream stream
            Expect.stringContains actual "I am totally a file" "incorrect server response"
        }

        testCaseAsync "Send file and get it back"
        <| async {
            let data = WebAPI.OperationTypes.PostApiReturnFileSingle_formData(toStream text)
            let! stream = api.PostApiReturnFileSingle(data)
            let! actual = fromStream stream
            Expect.equal actual text "incorrect server response"
        }

        testCaseAsync "Send form-with-file and get it back as IO.Stream"
        <| async {
            let data =
                WebAPI.OperationTypes.PostApiReturnFileFormWithFile_formData("newName.txt", toStream text)

            let! stream = api.PostApiReturnFileFormWithFile(data)
            let! actual = fromStream stream
            Expect.equal actual text "incorrect server response"
        }

        testCaseAsync "Send multiple files"
        <| async {
            let data =
                WebAPI.OperationTypes.PostApiReturnFileMultiple_formData([| toStream text; toStream text |])

            let! actual = api.PostApiReturnFileMultiple(data)
            Expect.equal actual 2 "incorrect server response"
        }

    ]
