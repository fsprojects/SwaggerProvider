module SwashbuckleReturnControllersTests

open Expecto
open SwaggerProvider
open System

type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1", IgnoreOperationId=true>
let api = WebAPI()

let shouldEqual expected actual =
    Expect.equal actual expected "return value"

[<Tests>]
let returnControllersTests =
  testList "All/Swashbuckle.ReturnControllers.Tests" [

    testCase "Return Bool GET Test" <| fun _ ->
        api.GetApiReturnBoolean()
        |> shouldEqual true

    testCase "Return Bool POST Test" <| fun _ ->
        api.PostApiReturnBoolean()
        |> shouldEqual true

    testCase "Return Int32 GET Test" <| fun _ ->
        api.GetApiReturnInt32()
        |> shouldEqual 42

    testCase "Return Int32 POST Test" <| fun _ ->
        api.PostApiReturnInt32()
        |> shouldEqual 42


    testCase "Return Int64 GET Test" <| fun _ ->
        api.GetApiReturnInt64()
        |> shouldEqual 42L

    testCase "Return Int64 POST Test" <| fun _ ->
        api.PostApiReturnInt64()
        |> shouldEqual 42L


    testCase "Return Float GET Test" <| fun _ ->
        api.GetApiReturnFloat()
        |> shouldEqual 42.0f

    testCase "Return Float POST Test" <| fun _ ->
        api.PostApiReturnFloat()
        |> shouldEqual 42.0f


    testCase "Return Double GET Test" <| fun _ ->
        api.GetApiReturnDouble()
        |> shouldEqual 42.0

    testCase "Return Double POST Test" <| fun _ ->
        api.PostApiReturnDouble()
        |> shouldEqual 42.0


    testCase "Return String GET Test" <| fun _ ->
        api.GetApiReturnString()
        |> shouldEqual "Hello world"

    testCase "Return String POST Test" <| fun _ ->
        api.PostApiReturnString()
        |> shouldEqual "Hello world"


    testCase "Return DateTime GET Test" <| fun _ ->
        api.GetApiReturnDateTime()
        |> shouldEqual (DateTime(2015,1,1))

    testCase "Return DateTime POST Test" <| fun _ ->
        api.PostApiReturnDateTime()
        |> shouldEqual (DateTime(2015,1,1))


    testCase "Return Enum GET Test" <| fun _ ->
        api.GetApiReturnEnum()
        |> shouldEqual "1"

    testCase "Return Enum GET Test" <| fun _ ->
        api.PostApiReturnEnum()
        |> shouldEqual "1"


    testCase "Return Array Int GET Test" <| fun _ ->
        api.GetApiReturnArrayInt()
        |> shouldEqual [|1;2;3|]

    testCase "Return Array Int POST Test" <| fun _ ->
        api.PostApiReturnArrayInt()
        |> shouldEqual [|1;2;3|]


    testCase "Return Array Enum GET Test" <| fun _ ->
        api.GetApiReturnArrayEnum()
        |> shouldEqual [|"1";"2"|]

    testCase "Return Array Enum POST Test" <| fun _ ->
        api.PostApiReturnArrayEnum()
        |> shouldEqual [|"1";"2"|]


    testCase "Return List Int GET Test" <| fun _ ->
        api.GetApiReturnListInt()
        |> shouldEqual [|1;2;3|]

    testCase "Return List Int POST Test" <| fun _ ->
        api.PostApiReturnListInt()
        |> shouldEqual [|1;2;3|]


    testCase "Return Seq Int GET Test" <| fun _ ->
        api.GetApiReturnSeqInt()
        |> shouldEqual [|1;2;3|]

    testCase "Return Seq Int POST Test" <| fun _ ->
        api.PostApiReturnSeqInt()
        |> shouldEqual [|1;2;3|]


    testCase "Return Object Point GET Test" <| fun _ ->
        let point = api.GetApiReturnObjectPointClass()
        point.X |> shouldEqual (Some(0))
        point.Y |> shouldEqual (Some(0))

    testCase "Return Object Point POST Test" <| fun _ ->
        let point = api.PostApiReturnObjectPointClass()
        point.X |> shouldEqual (Some(0))
        point.Y |> shouldEqual (Some(0))


    testCase "Return FileDescription GET Test" <| fun _ ->
        let file = api.GetApiReturnFileDescription()
        file.Name |> shouldEqual "1.txt"
        file.Bytes |> shouldEqual [|1uy;2uy;3uy|]

    testCase "Return FileDescription POST Test" <| fun _ ->
        let file = api.PostApiReturnFileDescription()
        file.Name |> shouldEqual "1.txt"
        file.Bytes |> shouldEqual [|1uy;2uy;3uy|]
  ]