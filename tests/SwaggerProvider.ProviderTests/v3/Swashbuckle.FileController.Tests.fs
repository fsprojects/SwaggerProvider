module Swashbuckle.v3.FileControllersTests

open Xunit
open FsUnitTyped
open System.IO
open Swashbuckle.v3.ReturnControllersTests

let text = "This is test file content"

let toStream(text: string) =
    let bytes = System.Text.Encoding.UTF8.GetBytes(text)
    new MemoryStream(bytes)

let fromStream(stream: Stream) = async {
    use reader = new StreamReader(stream)
    return! reader.ReadToEndAsync() |> Async.AwaitTask
}


[<Fact>]
let ``Download file as IO.Stream``() = task {
    let! stream = api.GetApiReturnFile()
    let! actual = fromStream stream
    actual |> shouldContainText "I am totally a file"
}

[<Fact>]
let ``Send file as IO.Stream``() = task {
    let bytes = System.Text.Encoding.UTF8.GetBytes("I am totally a file's\ncontent")
    use stream = new MemoryStream(bytes)
    let! actual = api.PostApiReturnFileStream(stream)
    actual |> shouldEqual bytes.Length
}

[<Fact>]
let ``Send file and get it back``() = task {
    let data = WebAPI.OperationTypes.PostApiReturnFileSingle_formData(toStream text)
    let! stream = api.PostApiReturnFileSingle(data)
    let! actual = fromStream stream
    actual |> shouldEqual text
}

[<Fact>]
let ``Send form-with-file and get it back as IO.Stream``() = task {
    let data =
        WebAPI.OperationTypes.PostApiReturnFileFormWithFile_formData("newName.txt", toStream text)

    let! stream = api.PostApiReturnFileFormWithFile(data)
    let! actual = fromStream stream
    actual |> shouldEqual text
}

[<Fact>]
let ``Send multiple files``() = task {
    let data =
        WebAPI.OperationTypes.PostApiReturnFileMultiple_formData([| toStream text; toStream text |])

    let! actual = api.PostApiReturnFileMultiple(data)
    actual |> shouldEqual 2
}
