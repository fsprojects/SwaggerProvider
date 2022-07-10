module Swashbuckle.v2.UpdateControllersTests

open Expecto
open System
open Swashbuckle.v2.ReturnControllersTests

[<Tests>]
let returnControllersTests =
    testList "All/v2/Swashbuckle.UpdateControllers.Tests" [

        testCaseAsync "Update Bool GET Test"
        <| (api.GetApiUpdateBool(Some true) |> asyncEqual false)

        testCaseAsync "Update Bool POST Test"
        <| (api.PostApiUpdateBool(Some false) |> asyncEqual true)


        testCaseAsync "Update Int32 GET Test"
        <| (api.GetApiUpdateInt32(Some 0) |> asyncEqual 1)

        testCaseAsync "Update Int32 POST Test"
        <| (api.PostApiUpdateInt32(Some 0) |> asyncEqual 1)


        testCaseAsync "Update Int64 GET Test"
        <| (api.GetApiUpdateInt64(Some 10L) |> asyncEqual 11L)

        testCaseAsync "Update Int64 POST Test"
        <| (api.PostApiUpdateInt64(Some 10L) |> asyncEqual 11L)

        testCaseAsync "Update Float GET Test"
        <| (api.GetApiUpdateFloat(Some 1.0f) |> asyncEqual 2.0f)

        testCaseAsync "Update Float POST Test"
        <| (api.PostApiUpdateFloat(Some 1.0f) |> asyncEqual 2.0f)


        testCaseAsync "Update Double GET Test"
        <| (api.GetApiUpdateDouble(Some 2.0) |> asyncEqual 3.0)

        testCaseAsync "Update Double POST Test"
        <| (api.PostApiUpdateDouble(Some 2.0) |> asyncEqual 3.0)


        testCaseAsync "Update String GET Test"
        <| (api.GetApiUpdateString("Serge") |> asyncEqual "Hello, Serge")

        testCaseAsync "Update String POST Test"
        <| (api.PostApiUpdateString("Serge") |> asyncEqual "Hello, Serge")


        testCaseAsync "Update DateTime GET Test"
        <| (api.GetApiUpdateDateTime(Some <| DateTime(2015, 1, 1))
            |> asyncEqual(DateTime(2015, 1, 2)))

        testCaseAsync "Update DateTime POST Test"
        <| (api.PostApiUpdateDateTime(Some <| DateTime(2015, 1, 1))
            |> asyncEqual(DateTime(2015, 1, 2)))


        testCaseAsync "Update Enum GET Test"
        <| (api.GetApiUpdateEnum(Some 1) |> asyncEqual 1)

        testCaseAsync "Update Enum POST Test"
        <| (api.PostApiUpdateEnum(Some 1) |> asyncEqual 1)


        testCaseAsync "Update Array Int GET Test"
        <| (api.GetApiUpdateArrayInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])

        testCaseAsync "Update Array Int POST Test"
        <| (api.PostApiUpdateArrayInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])


        testCaseAsync "Update Array Enum GET Test"
        <| (api.GetApiUpdateArrayEnum([| 2; 1 |]) |> asyncEqual [| 1; 2 |])

        testCaseAsync "Update Array Enum POST Test"
        <| (api.PostApiUpdateArrayEnum([| 2; 1 |]) |> asyncEqual [| 1; 2 |])


        //TODO:   testCaseAsync "Update Bool GET Test" <|  // "ExceptionMessage":"No parameterless constructor defined for this object."
        //let ``Update List Int GET Test`` () =
        //    WebAPI.UpdateListInt.Get([|3;2;1|])
        //    |> asyncEqual [|1;2;3|]

        testCaseAsync "Update List Int POST Test"
        <| (api.PostApiUpdateListInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])


        testCaseAsync "Update Seq Int GET Test"
        <| (api.GetApiUpdateSeqInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])

        testCaseAsync "Update Seq Int POST Test"
        <| (api.PostApiUpdateSeqInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])


        // TODO: Server return point (0,0)
        // testCaseAsync "Update Object Point GET Test" <| async {
        //     let! point = api.GetApiUpdateObjectPointClass(x = Some 1, y = Some 2)
        //     point.X |> shouldEqual (Some 2)
        //     point.Y |> shouldEqual (Some 1)
        // }

        testCaseAsync "Update Object Point POST Test"
        <| async {
            let p = WebAPI.PointClass()
            p.X <- Some 1
            p.Y <- Some 2
            let! point = api.PostApiUpdateObjectPointClass(p)
            point.X |> shouldEqual(Some 2)
            point.Y |> shouldEqual(Some 1)
        }

        testCaseAsync "Send and Receive object with byte[]"
        <| async {
            let x = WebAPI.FileDescription(Name = "2.txt", Bytes = [| 42uy |])
            let! y = api.PostApiUpdateObjectFileDescriptionClass(x)
            x.Name |> shouldEqual y.Name
            x.Bytes |> shouldEqual y.Bytes
        }

        // System.Net.Http.HttpRequestException: Response status code does not indicate success: 400 (Bad Request).
        // testCaseAsync "Send byte[] in query" <| async {
        //     let bytes = [|42uy;24uy|]
        //     let! y = api.GetApiUpdateObjectFileDescriptionClass(bytes)
        //     y.Bytes |> shouldEqual (bytes)
        // }

        testCaseAsync "Use Optional param Int"
        <| async {
            do! api.GetApiUpdateWithOptionalInt(Some 1) |> asyncEqual 2
            do! api.GetApiUpdateWithOptionalInt(Some 1, Some 2) |> asyncEqual 3
        }
    ]
