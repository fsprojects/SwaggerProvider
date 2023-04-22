module Swashbuckle.v3.ReturnControllersTests

open Xunit
open FsUnitTyped
open SwaggerProvider
open System
open System.Net.Http

type CallLoggingHandler(messageHandler) =
    inherit DelegatingHandler(messageHandler)

    override _.SendAsync(request, cancellationToken) =
        printfn $"[SendAsync]: %A{request.RequestUri}"
        base.SendAsync(request, cancellationToken)

type WebAPI = OpenApiClientProvider<"http://localhost:5000/swagger/v1/openapi.json", IgnoreOperationId=true>

let api =
    let handler = new HttpClientHandler(UseCookies = false)

    let client =
        new HttpClient(handler, true, BaseAddress = Uri("http://localhost:5000"))

    WebAPI.Client(client)

let asyncEqual expected actualTask =
    task {
        let! actual = actualTask
        actual |> shouldEqual expected
    }

[<Fact>]
let ``Return Bool GET Test``() =
    api.GetApiReturnBoolean() |> asyncEqual true

[<Fact>]
let ``Return Bool POST Test``() =
    api.PostApiReturnBoolean() |> asyncEqual true

[<Fact>]
let ``Return Int32 GET Test"``() =
    api.GetApiReturnInt32() |> asyncEqual 42

[<Fact>]
let ``Return Int32 POST Test``() =
    api.PostApiReturnInt32() |> asyncEqual 42


[<Fact>]
let ``Return Int64 GET Test``() =
    api.GetApiReturnInt64() |> asyncEqual 42L

[<Fact>]
let ``Return Int64 POST Test``() =
    api.PostApiReturnInt64() |> asyncEqual 42L

[<Fact>]
let ``Return Float GET Test``() =
    api.GetApiReturnFloat() |> asyncEqual 42.0f

[<Fact>]
let ``Return Float POST Test``() =
    api.PostApiReturnFloat() |> asyncEqual 42.0f


[<Fact>]
let ``Return Double GET Test``() =
    api.GetApiReturnDouble() |> asyncEqual 42.0

[<Fact>]
let ``Return Double POST Test``() =
    api.PostApiReturnDouble() |> asyncEqual 42.0


[<Fact>]
let ``Return String GET Test``() =
    api.GetApiReturnString() |> asyncEqual "Hello world"

[<Fact>]
let ``Return String POST Test``() =
    api.PostApiReturnString() |> asyncEqual "Hello world"


[<Fact>]
let ``Return DateTime GET Test``() =
    api.GetApiReturnDateTime()
    |> asyncEqual(DateTimeOffset <| DateTime(2015, 1, 1))

[<Fact>]
let ``Return DateTime POST Test``() =
    api.PostApiReturnDateTime()
    |> asyncEqual(DateTimeOffset <| DateTime(2015, 1, 1))

[<Fact>]
let ``Return Guid GET Test``() =
    api.GetApiReturnGuid() |> asyncEqual(Guid.Empty)

[<Fact>]
let ``Return Guid POST Test``() =
    api.PostApiReturnGuid() |> asyncEqual(Guid.Empty)

[<Fact>]
let ``Return Enum GET Test``() =
    api.GetApiReturnEnum() |> asyncEqual "Absolute"

[<Fact>]
let ``Return Enum POST Test``() =
    api.PostApiReturnEnum() |> asyncEqual "Absolute"


[<Fact>]
let ``Return Array Int GET Test``() =
    api.GetApiReturnArrayInt() |> asyncEqual [| 1; 2; 3 |]

[<Fact>]
let ``Return Array Int POST Test``() =
    api.PostApiReturnArrayInt() |> asyncEqual [| 1; 2; 3 |]


[<Fact>]
let ``Return Array Enum GET Test``() =
    api.GetApiReturnArrayEnum() |> asyncEqual [| "Absolute"; "Relative" |]

[<Fact>]
let ``Return Array Enum POST Test``() =
    api.PostApiReturnArrayEnum()
    |> asyncEqual [| "Absolute"; "Relative" |]


[<Fact>]
let ``Return List Int GET Test``() =
    api.GetApiReturnListInt() |> asyncEqual [| 1; 2; 3 |]

[<Fact>]
let ``Return List Int POST Test``() =
    api.PostApiReturnListInt() |> asyncEqual [| 1; 2; 3 |]


[<Fact>]
let ``Return Seq Int GET Test``() =
    api.GetApiReturnSeqInt() |> asyncEqual [| 1; 2; 3 |]

[<Fact>]
let ``Return Seq Int POST Test``() =
    api.PostApiReturnSeqInt() |> asyncEqual [| 1; 2; 3 |]


[<Fact>]
let ``Return Object Point GET Test``() =
    task {
        let! point = api.GetApiReturnObjectPointClass()
        point.X |> shouldEqual(Some 0)
        point.Y |> shouldEqual(Some 0)
    }

[<Fact>]
let ``Return Object Point POST Test``() =
    task {
        let! point = api.PostApiReturnObjectPointClass()
        point.X |> shouldEqual(Some 0)
        point.Y |> shouldEqual(Some 0)
    }


[<Fact>]
let ``Return FileDescription GET Test``() =
    task {
        let! file = api.GetApiReturnFileDescription()
        file.Name |> shouldEqual("1.txt")
        file.Bytes |> shouldEqual([| 1uy; 2uy; 3uy |])
    }

[<Fact>]
let ``Return FileDescription POST Test``() =
    task {
        let! file = api.PostApiReturnFileDescription()
        file.Name |> shouldEqual("1.txt")
        file.Bytes |> shouldEqual([| 1uy; 2uy; 3uy |])
    }

[<Fact>]
let ``Return String Dictionary GET Test``() =
    task {
        let! dict = api.GetApiReturnStringDictionary()
        dict |> shouldEqual(Map [ "hello", "world" ])
    }

[<Fact>]
let ``Return Object Point Dictionary GET Test``() =
    task {
        let! dict = api.GetApiReturnObjectPointClassDictionary()
        dict.ContainsKey "point" |> shouldEqual true
        let point = dict.["point"]
        point.X |> shouldEqual(Some 0)
        point.Y |> shouldEqual(Some 0)
    }
