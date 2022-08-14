module Swashbuckle.v2.ReturnControllersTests

open Expecto
open SwaggerProvider
open System
open System.Net.Http

type WebAPI = SwaggerClientProvider<"http://localhost:5000/swagger/v1/swagger.json", IgnoreOperationId=true, PreferAsync=true>

let api =
    let handler = new HttpClientHandler(UseCookies = false)
    handler.ServerCertificateCustomValidationCallback <- Func<_, _, _, _, _>(fun a b c d -> true)

    let client =
        new HttpClient(handler, true, BaseAddress = Uri("http://localhost:5000"))

    WebAPI.Client(client)

let shouldEqual expected actual =
    Expect.equal actual expected "return value"

let asyncEqual expected actual = async {
    let! actual' = actual
    shouldEqual expected actual'
}

[<Tests>]
let returnControllersTests =
    testList "All/v2/Swashbuckle.ReturnControllers.Tests" [

        testCaseAsync "Return Bool GET Test"
        <| (api.GetApiReturnBoolean() |> asyncEqual true)

        testCaseAsync "Return Bool POST Test"
        <| (api.PostApiReturnBoolean() |> asyncEqual true)


        testCaseAsync "Return Int32 GET Test"
        <| (api.GetApiReturnInt32() |> asyncEqual 42)

        testCaseAsync "Return Int32 POST Test"
        <| (api.PostApiReturnInt32() |> asyncEqual 42)


        testCaseAsync "Return Int64 GET Test"
        <| (api.GetApiReturnInt64() |> asyncEqual 42L)

        testCaseAsync "Return Int64 POST Test"
        <| (api.PostApiReturnInt64() |> asyncEqual 42L)

        testCaseAsync "Return Float GET Test"
        <| (api.GetApiReturnFloat() |> asyncEqual 42.0f)

        testCaseAsync "Return Float POST Test"
        <| (api.PostApiReturnFloat() |> asyncEqual 42.0f)


        testCaseAsync "Return Double GET Test"
        <| (api.GetApiReturnDouble() |> asyncEqual 42.0)

        testCaseAsync "Return Double POST Test"
        <| (api.PostApiReturnDouble() |> asyncEqual 42.0)


        testCaseAsync "Return String GET Test"
        <| (api.GetApiReturnString() |> asyncEqual "Hello world")

        testCaseAsync "Return String POST Test"
        <| (api.PostApiReturnString() |> asyncEqual "Hello world")


        testCaseAsync "Return DateTime GET Test"
        <| (api.GetApiReturnDateTime() |> asyncEqual(DateTime(2015, 1, 1)))

        testCaseAsync "Return DateTime POST Test"
        <| (api.PostApiReturnDateTime() |> asyncEqual(DateTime(2015, 1, 1)))


        testCaseAsync "Return Enum GET Test"
        <| (api.GetApiReturnEnum() |> asyncEqual "Absolute")

        testCaseAsync "Return Enum POST Test"
        <| (api.PostApiReturnEnum() |> asyncEqual "Absolute")


        testCaseAsync "Return Array Int GET Test"
        <| (api.GetApiReturnArrayInt() |> asyncEqual [| 1; 2; 3 |])

        testCaseAsync "Return Array Int POST Test"
        <| (api.PostApiReturnArrayInt() |> asyncEqual [| 1; 2; 3 |])


        testCaseAsync "Return Array Enum GET Test"
        <| (api.GetApiReturnArrayEnum() |> asyncEqual [| "Absolute"; "Relative" |])

        testCaseAsync "Return Array Enum POST Test"
        <| (api.PostApiReturnArrayEnum()
            |> asyncEqual [| "Absolute"; "Relative" |])


        testCaseAsync "Return List Int GET Test"
        <| (api.GetApiReturnListInt() |> asyncEqual [| 1; 2; 3 |])

        testCaseAsync "Return List Int POST Test"
        <| (api.PostApiReturnListInt() |> asyncEqual [| 1; 2; 3 |])


        testCaseAsync "Return Seq Int GET Test"
        <| (api.GetApiReturnSeqInt() |> asyncEqual [| 1; 2; 3 |])

        testCaseAsync "Return Seq Int POST Test"
        <| (api.PostApiReturnSeqInt() |> asyncEqual [| 1; 2; 3 |])


        testCaseAsync "Return Object Point GET Test"
        <| async {
            let! point = api.GetApiReturnObjectPointClass()
            point.X |> shouldEqual(Some 0)
            point.Y |> shouldEqual(Some 0)
        }

        testCaseAsync "Return Object Point POST Test"
        <| async {
            let! point = api.PostApiReturnObjectPointClass()
            point.X |> shouldEqual(Some 0)
            point.Y |> shouldEqual(Some 0)
        }


        testCaseAsync "Return FileDescription GET Test"
        <| async {
            let! file = api.GetApiReturnFileDescription()
            file.Name |> shouldEqual("1.txt")
            file.Bytes |> shouldEqual([| 1uy; 2uy; 3uy |])
        }

        testCaseAsync "Return FileDescription POST Test"
        <| async {
            let! file = api.PostApiReturnFileDescription()
            file.Name |> shouldEqual("1.txt")
            file.Bytes |> shouldEqual([| 1uy; 2uy; 3uy |])
        }
    ]
