module SwashbuckleReturnControllersTests

open NUnit.Framework
open FsUnit
open SwaggerProvider
open System

type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1">

[<Test>]
let ``Return Bool GET Test`` () =
    WebAPI.ReturnBoolean.Get()
    |> should be True

[<Test>]
let ``Return Bool POST Test`` () =
    WebAPI.ReturnBoolean.Post()
    |> should be True


[<Test>]
let ``Return Int32 GET Test`` () =
    WebAPI.ReturnInt32.Get()
    |> should equal 42

[<Test>]
let ``Return Int32 POST Test`` () =
    WebAPI.ReturnInt32.Post()
    |> should equal 42


[<Test>]
let ``Return Int64 GET Test`` () =
    WebAPI.ReturnInt64.Get()
    |> should equal 42L

[<Test>]
let ``Return Int64 POST Test`` () =
    WebAPI.ReturnInt64.Post()
    |> should equal 42L


[<Test>]
let ``Return Float GET Test`` () =
    WebAPI.ReturnFloat.Get()
    |> should equal 42.0f

[<Test>]
let ``Return Float POST Test`` () =
    WebAPI.ReturnFloat.Post()
    |> should equal 42.0f


[<Test>]
let ``Return Double GET Test`` () =
    WebAPI.ReturnDouble.Get()
    |> should equal 42.0

[<Test>]
let ``Return Double POST Test`` () =
    WebAPI.ReturnDouble.Post()
    |> should equal 42.0


[<Test>]
let ``Return String GET Test`` () =
    WebAPI.ReturnString.Get()
    |> should equal "Hello world"

[<Test>]
let ``Return String POST Test`` () =
    WebAPI.ReturnString.Post()
    |> should equal "Hello world"


[<Test>]
let ``Return DateTime GET Test`` () =
    WebAPI.ReturnDateTime.Get()
    |> should equal (DateTime(2015,1,1))

[<Test>]
let ``Return DateTime POST Test`` () =
    WebAPI.ReturnDateTime.Post()
    |> should equal (DateTime(2015,1,1))


[<Test>]
let ``Return Enum GET Test`` () =
    WebAPI.ReturnEnum.Get()
    |> should equal "1"

[<Test>]
let ``Return Enum POST Test`` () =
    WebAPI.ReturnEnum.Post()
    |> should equal "1"


[<Test>]
let ``Return Array Int GET Test`` () =
    WebAPI.ReturnArrayInt.Get()
    |> should equal [|1;2;3|]

[<Test>]
let ``Return Array Int POST Test`` () =
    WebAPI.ReturnArrayInt.Post()
    |> should equal [|1;2;3|]


[<Test>]
let ``Return Array Enum GET Test`` () =
    WebAPI.ReturnArrayEnum.Get()
    |> should equal [|"1";"2"|]

[<Test>]
let ``Return Array Enum POST Test`` () =
    WebAPI.ReturnArrayEnum.Post()
    |> should equal [|"1";"2"|]


[<Test>]
let ``Return List Int GET Test`` () =
    WebAPI.ReturnListInt.Get()
    |> should equal [|1;2;3|]

[<Test>]
let ``Return List Int POST Test`` () =
    WebAPI.ReturnListInt.Post()
    |> should equal [|1;2;3|]


[<Test>]
let ``Return Seq Int GET Test`` () =
    WebAPI.ReturnSeqInt.Get()
    |> should equal [|1;2;3|]

[<Test>]
let ``Return Seq Int POST Test`` () =
    WebAPI.ReturnSeqInt.Post()
    |> should equal [|1;2;3|]


[<Test>]
let ``Return Object Point GET Test`` () =
    let point = WebAPI.ReturnObjectPointClass.Get()
    point.X |> should equal (Some(0))
    point.Y |> should equal (Some(0))

[<Test>]
let ``Return Object Point POST Test`` () =
    let point = WebAPI.ReturnObjectPointClass.Post()
    point.X |> should equal (Some(0))
    point.Y |> should equal (Some(0))
