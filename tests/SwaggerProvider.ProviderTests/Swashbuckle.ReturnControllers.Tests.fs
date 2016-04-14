module SwashbuckleReturnControllersTests

open NUnit.Framework
open FsUnitTyped
open SwaggerProvider
open System

type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1", IgnoreOperationId=true>
let api = WebAPI()

[<Test>]
let ``Return Bool GET Test`` () =
    api.GetApiReturnBoolean()
    |> shouldEqual true

[<Test>]
let ``Return Bool POST Test`` () =
    api.PostApiReturnBoolean()
    |> shouldEqual true


[<Test>]
let ``Return Int32 GET Test`` () =
    api.GetApiReturnInt32()
    |> shouldEqual 42

[<Test>]
let ``Return Int32 POST Test`` () =
    api.PostApiReturnInt32()
    |> shouldEqual 42


[<Test>]
let ``Return Int64 GET Test`` () =
    api.GetApiReturnInt64()
    |> shouldEqual 42L

[<Test>]
let ``Return Int64 POST Test`` () =
    api.PostApiReturnInt64()
    |> shouldEqual 42L


[<Test>]
let ``Return Float GET Test`` () =
    api.GetApiReturnFloat()
    |> shouldEqual 42.0f

[<Test>]
let ``Return Float POST Test`` () =
    api.PostApiReturnFloat()
    |> shouldEqual 42.0f


[<Test>]
let ``Return Double GET Test`` () =
    api.GetApiReturnDouble()
    |> shouldEqual 42.0

[<Test>]
let ``Return Double POST Test`` () =
    api.PostApiReturnDouble()
    |> shouldEqual 42.0


[<Test>]
let ``Return String GET Test`` () =
    api.GetApiReturnString()
    |> shouldEqual "Hello world"

[<Test>]
let ``Return String POST Test`` () =
    api.PostApiReturnString()
    |> shouldEqual "Hello world"


[<Test>]
let ``Return DateTime GET Test`` () =
    api.GetApiReturnDateTime()
    |> shouldEqual (DateTime(2015,1,1))

[<Test>]
let ``Return DateTime POST Test`` () =
    api.PostApiReturnDateTime()
    |> shouldEqual (DateTime(2015,1,1))


[<Test>]
let ``Return Enum GET Test`` () =
    api.GetApiReturnEnum()
    |> shouldEqual "1"

[<Test>]
let ``Return Enum POST Test`` () =
    api.PostApiReturnEnum()
    |> shouldEqual "1"


[<Test>]
let ``Return Array Int GET Test`` () =
    api.GetApiReturnArrayInt()
    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Return Array Int POST Test`` () =
    api.PostApiReturnArrayInt()
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Return Array Enum GET Test`` () =
    api.GetApiReturnArrayEnum()
    |> shouldEqual [|"1";"2"|]

[<Test>]
let ``Return Array Enum POST Test`` () =
    api.PostApiReturnArrayEnum()
    |> shouldEqual [|"1";"2"|]


[<Test>]
let ``Return List Int GET Test`` () =
    api.GetApiReturnListInt()
    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Return List Int POST Test`` () =
    api.PostApiReturnListInt()
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Return Seq Int GET Test`` () =
    api.GetApiReturnSeqInt()
    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Return Seq Int POST Test`` () =
    api.PostApiReturnSeqInt()
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Return Object Point GET Test`` () =
    let point = api.GetApiReturnObjectPointClass()
    point.X |> shouldEqual (Some(0))
    point.Y |> shouldEqual (Some(0))

[<Test>]
let ``Return Object Point POST Test`` () =
    let point = api.PostApiReturnObjectPointClass()
    point.X |> shouldEqual (Some(0))
    point.Y |> shouldEqual (Some(0))
