module SwashbuckleReturnControllersTests

open Expecto
open SwaggerProvider
open System

type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1", IgnoreOperationId=true, AsyncInsteadOfTask = true>
let api = WebAPI.Client()

let shouldEqual expected actual =
    Expect.equal actual expected "return value"

let asyncEqual expected actual = 
    async {
        let! actual' = actual
        shouldEqual expected actual'
    }

[<Tests>]
let returnControllersTests =
  testList "All/Swashbuckle.ReturnControllers.Tests" [

    testCaseAsync "Return Bool GET Test" <| 
        (api.GetApiReturnBooleanAsync()
         |> asyncEqual true)

    testCaseAsync "Return Bool POST Test" <| 
        (api.PostApiReturnBooleanAsync()
         |> asyncEqual true)


    testCaseAsync "Return Int32 GET Test" <| 
        (api.GetApiReturnInt32Async()
         |> asyncEqual 42)

    testCaseAsync "Return Int32 POST Test" <| 
        (api.PostApiReturnInt32Async()
         |> asyncEqual 42)


    testCaseAsync "Return Int64 GET Test" <| 
        (api.GetApiReturnInt64Async()
         |> asyncEqual 42L)

    testCaseAsync "Return Int64 POST Test" <| 
        (api.PostApiReturnInt64Async()
         |> asyncEqual 42L)


    testCaseAsync "Return Float GET Test" <| 
        (api.GetApiReturnFloatAsync()
         |> asyncEqual 42.0f)

    testCaseAsync "Return Float POST Test" <| 
        (api.PostApiReturnFloatAsync()
         |> asyncEqual 42.0f)


    testCaseAsync "Return Double GET Test" <| 
        (api.GetApiReturnDoubleAsync()
         |> asyncEqual 42.0)

    testCaseAsync "Return Double POST Test" <| 
        (api.PostApiReturnDoubleAsync()
         |> asyncEqual 42.0)


    testCaseAsync "Return String GET Test" <| 
        (api.GetApiReturnStringAsync()
         |> asyncEqual "Hello world")

    testCaseAsync "Return String POST Test" <| 
        (api.PostApiReturnStringAsync()
         |> asyncEqual "Hello world")


    testCaseAsync "Return DateTime GET Test" <| 
        (api.GetApiReturnDateTimeAsync()
         |> asyncEqual (DateTime(2015,1,1)))

    testCaseAsync "Return DateTime POST Test" <| 
        (api.PostApiReturnDateTimeAsync()
         |> asyncEqual (DateTime(2015,1,1)))


    testCaseAsync "Return Enum GET Test" <| 
        (api.GetApiReturnEnumAsync()
         |> asyncEqual "1")

    testCaseAsync "Return Enum POST Test" <| 
        (api.PostApiReturnEnumAsync()
         |> asyncEqual "1")


    testCaseAsync "Return Array Int GET Test" <| 
        (api.GetApiReturnArrayIntAsync()
         |> asyncEqual [|1;2;3|])

    testCaseAsync "Return Array Int POST Test" <| 
        (api.PostApiReturnArrayIntAsync()
         |> asyncEqual [|1;2;3|])


    testCaseAsync "Return Array Enum GET Test" <| 
        (api.GetApiReturnArrayEnumAsync()
         |> asyncEqual [|"1";"2"|])

    testCaseAsync "Return Array Enum POST Test" <| 
        (api.PostApiReturnArrayEnumAsync()
         |> asyncEqual [|"1";"2"|])


    testCaseAsync "Return List Int GET Test" <| 
        (api.GetApiReturnListIntAsync()
         |> asyncEqual [|1;2;3|])

    testCaseAsync "Return List Int POST Test" <| 
        (api.PostApiReturnListIntAsync()
         |> asyncEqual [|1;2;3|])


    testCaseAsync "Return Seq Int GET Test" <| 
        (api.GetApiReturnSeqIntAsync()
         |> asyncEqual [|1;2;3|])

    testCaseAsync "Return Seq Int POST Test" <| 
        (api.PostApiReturnSeqIntAsync()
         |> asyncEqual [|1;2;3|])


    testCaseAsync "Return Object Point GET Test" <| async {
        let! point = api.GetApiReturnObjectPointClassAsync()
        point.X  |> shouldEqual (Some(0))
        point.Y  |> shouldEqual (Some(0))
    }

    testCaseAsync "Return Object Point POST Test" <| async {
        let! point = api.PostApiReturnObjectPointClassAsync()
        point.X  |> shouldEqual (Some(0))
        point.Y  |> shouldEqual (Some(0))
    }


    testCaseAsync "Return FileDescription GET Test" <| async {
        let! file = api.GetApiReturnFileDescriptionAsync()
        file.Name  |> shouldEqual (Some "1.txt")
        file.Bytes  |> shouldEqual (Some [|1uy;2uy;3uy|])
    }

    testCaseAsync "Return FileDescription POST Test" <| async {
        let! file = api.PostApiReturnFileDescriptionAsync()
        file.Name  |> shouldEqual (Some "1.txt")
        file.Bytes  |> shouldEqual (Some [|1uy;2uy;3uy|])
    }
  ]