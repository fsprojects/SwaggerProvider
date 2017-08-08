module SwashbuckleUpdateControllersTests

open Expecto
open System
open SwashbuckleReturnControllersTests

[<Tests>]
let returnControllersTests =
  testList "All/Swashbuckle.UpdateControllers.Tests" [

    testCase "Update Bool GET Test" <| fun _ ->
        api.GetApiUpdateBool(true)
        |> shouldEqual false

    testCase "Update Bool POST Test" <| fun _ ->
        api.PostApiUpdateBool(false)
        |> shouldEqual true


    testCase "Update Int32 GET Test" <| fun _ ->
        api.GetApiUpdateInt32(0)
        |> shouldEqual 1

    testCase "Update Int32 POST Test" <| fun _ ->
        api.PostApiUpdateInt32(0)
        |> shouldEqual 1


    testCase "Update Int64 GET Test" <| fun _ ->
        api.GetApiUpdateInt64(10L)
        |> shouldEqual 11L

    testCase "Update Int64 POST Test" <| fun _ ->
        api.PostApiUpdateInt64(10L)
        |> shouldEqual 11L


    testCase "Update Float GET Test" <| fun _ ->
        api.GetApiUpdateFloat(1.0f)
        |> shouldEqual 2.0f

    testCase "Update Float POST Test" <| fun _ ->
        api.PostApiUpdateFloat(1.0f)
        |> shouldEqual 2.0f


    testCase "Update Double GET Test" <| fun _ ->
        api.GetApiUpdateDouble(2.0)
        |> shouldEqual 3.0

    testCase "Update Double POST Test" <| fun _ ->
        api.PostApiUpdateDouble(2.0)
        |> shouldEqual 3.0


    testCase "Update String GET Test" <| fun _ ->
        api.GetApiUpdateString("Serge")
        |> shouldEqual "Hello, Serge"

    testCase "Update String POST Test" <| fun _ ->
        api.PostApiUpdateString("Serge")
        |> shouldEqual "Hello, Serge"


    testCase "Update DateTime GET Test" <| fun _ ->
        api.GetApiUpdateDateTime(DateTime(2015,1,1))
        |> shouldEqual (DateTime(2015,1,2))

    testCase "Update DateTime POST Test" <| fun _ ->
        api.PostApiUpdateDateTime(DateTime(2015,1,1))
        |> shouldEqual (DateTime(2015,1,2))


    testCase "Update Enum GET Test" <| fun _ ->
        api.GetApiUpdateEnum("1")
        |> shouldEqual "1"

    testCase "Update Enum POST Test" <| fun _ ->
        api.PostApiUpdateEnum("1")
        |> shouldEqual "1"


    testCase "Update Array Int GET Test" <| fun _ ->
        api.GetApiUpdateArrayInt([|3;2;1|])
        |> shouldEqual [|1;2;3|]

    testCase "Update Array Int POST Test" <| fun _ ->
        api.PostApiUpdateArrayInt([|3;2;1|])
        |> shouldEqual [|1;2;3|]


    testCase "Update Array Enum GET Test" <| fun _ ->
        api.GetApiUpdateArrayEnum([|"2";"1"|])
        |> shouldEqual [|"1";"2"|]

    testCase "Update Array Enum POST Test" <| fun _ ->
        api.PostApiUpdateArrayEnum([|"2";"1"|])
        |> shouldEqual [|"1";"2"|]


//    testCase "Update Bool GET Test" <| fun _ -> // "ExceptionMessage":"No parameterless constructor defined for this object."
//let ``Update List Int GET Test`` () =
//    WebAPI.UpdateListInt.Get([|3;2;1|])
//    |> shouldEqual [|1;2;3|]

    testCase "Update List Int POST Test" <| fun _ ->
        api.PostApiUpdateListInt([|3;2;1|])
        |> shouldEqual [|1;2;3|]


    testCase "Update Seq Int GET Test" <| fun _ ->
        api.GetApiUpdateSeqInt([|3;2;1|])
        |> shouldEqual [|1;2;3|]

    testCase "Update Seq Int POST Test" <| fun _ ->
        api.PostApiUpdateSeqInt([|3;2;1|])
        |> shouldEqual [|1;2;3|]


    testCase "Update Object Point GET Test" <| fun _ ->
        let point = api.GetApiUpdateObjectPointClass(xX = Some 1, xY = Some 2)
        point.X |> shouldEqual (Some 2)
        point.Y |> shouldEqual (Some 1)

    testCase "Update Object Point POST Test" <| fun _ ->
        let p = WebAPI.PointClass()
        p.X <- Some 1
        p.Y <- Some 2
        let point = api.PostApiUpdateObjectPointClass(p)
        point.X |> shouldEqual (Some 2)
        point.Y |> shouldEqual (Some 1)

    testCase "Send and Receive object with byte[]" <| fun _ ->
        let x = WebAPI.FileDescription(Name="2.txt", Bytes=[|42uy|])
        let y = api.PostApiUpdateObjectFileDescriptionClass(x)
        x.Name |> shouldEqual y.Name
        x.Bytes|> shouldEqual y.Bytes

    testCase "Send byte[] in query" <| fun _ ->
        let bytes = [|42uy;24uy|]
        let y = api.GetApiUpdateObjectFileDescriptionClass(bytes)
        y.Bytes |> shouldEqual bytes

    testCase "Use Optional param Int" <| fun _ ->
        api.GetApiUpdateWithOptionalInt(1)
        |> shouldEqual 2

        api.GetApiUpdateWithOptionalInt(1, Some(2))
        |> shouldEqual 3
  ]