module SwashbuckleUpdateControllersTests

open NUnit.Framework
open FsUnit
open System
open SwashbuckleReturnControllersTests

[<Test>]
let ``Update Bool GET Test`` () =
    WebAPI.UpdateBool.Get(true)
    |> should be False

[<Test>]
let ``Update Bool POST Test`` () =
    WebAPI.UpdateBool.Post(false)
    |> should be True


[<Test>]
let ``Update Int32 GET Test`` () =
    WebAPI.UpdateInt32.Get(0)
    |> should equal 1

[<Test>]
let ``Update Int32 POST Test`` () =
    WebAPI.UpdateInt32.Post(0)
    |> should equal 1


[<Test>]
let ``Update Int64 GET Test`` () =
    WebAPI.UpdateInt64.Get(10L)
    |> should equal 11L

[<Test>]
let ``Update Int64 POST Test`` () =
    WebAPI.UpdateInt64.Post(10L)
    |> should equal 11L


[<Test>]
let ``Update Float GET Test`` () =
    WebAPI.UpdateFloat.Get(1.0f)
    |> should equal 2.0f

[<Test>]
let ``Update Float POST Test`` () =
    WebAPI.UpdateFloat.Post(1.0f)
    |> should equal 2.0f


[<Test>]
let ``Update Double GET Test`` () =
    WebAPI.UpdateDouble.Get(2.0)
    |> should equal 3.0

[<Test>]
let ``Update Double POST Test`` () =
    WebAPI.UpdateDouble.Post(2.0)
    |> should equal 3.0


[<Test>]
let ``Update String GET Test`` () =
    WebAPI.UpdateString.Get("Serge")
    |> should equal "Hello, Serge"

[<Test>]
let ``Update String POST Test`` () =
    WebAPI.UpdateString.Post("Serge")
    |> should equal "Hello, Serge"


[<Test>]
let ``Update DateTime GET Test`` () =
    WebAPI.UpdateDateTime.Get(DateTime(2015,1,1))
    |> should equal (DateTime(2015,1,2))

[<Test>]
let ``Update DateTime POST Test`` () =
    WebAPI.UpdateDateTime.Post(DateTime(2015,1,1))
    |> should equal (DateTime(2015,1,2))


[<Test>]
let ``Update Enum GET Test`` () =
    WebAPI.UpdateEnum.Get("1")
    |> should equal "1"

[<Test>]
let ``Update Enum POST Test`` () =
    WebAPI.UpdateEnum.Post("1")
    |> should equal "1"


[<Test>]
let ``Update Array Int GET Test`` () =
    WebAPI.UpdateArrayInt.Get([|3;2;1|])
    |> should equal [|1;2;3|]

[<Test>]
let ``Update Array Int POST Test`` () =
    WebAPI.UpdateArrayInt.Post([|3;2;1|])
    |> should equal [|1;2;3|]


[<Test>]
let ``Update Array Enum GET Test`` () =
    WebAPI.UpdateArrayEnum.Get([|"2";"1"|])
    |> should equal [|"1";"2"|]

[<Test>]
let ``Update Array Enum POST Test`` () =
    WebAPI.UpdateArrayEnum.Post([|"2";"1"|])
    |> should equal [|"1";"2"|]


//[<Test>] // "ExceptionMessage":"No parameterless constructor defined for this object."
//let ``Update List Int GET Test`` () =
//    WebAPI.UpdateListInt.Get([|3;2;1|])
//    |> should equal [|1;2;3|]

[<Test>]
let ``Update List Int POST Test`` () =
    WebAPI.UpdateListInt.Post([|3;2;1|])
    |> should equal [|1;2;3|]


[<Test>]
let ``Update Seq Int GET Test`` () =
    WebAPI.UpdateSeqInt.Get([|3;2;1|])
    |> should equal [|1;2;3|]

[<Test>]
let ``Update Seq Int POST Test`` () =
    WebAPI.UpdateSeqInt.Post([|3;2;1|])
    |> should equal [|1;2;3|]


[<Test>]
let ``Update Object Point GET Test`` () =
    let point = WebAPI.UpdateObjectPointClass.Get(xX = Some 1, xY = Some 2)
    point.X |> should equal (Some 2)
    point.Y |> should equal (Some 1)

[<Test>]
let ``Update Object Point POST Test`` () =
    let p = WebAPI.Definitions.PointClass()
    p.X <- Some 1
    p.Y <- Some 2
    let point = WebAPI.UpdateObjectPointClass.Post(p)
    point.X |> should equal (Some 2)
    point.Y |> should equal (Some 1)
