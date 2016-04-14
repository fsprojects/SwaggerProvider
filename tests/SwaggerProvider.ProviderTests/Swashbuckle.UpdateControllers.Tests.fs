module SwashbuckleUpdateControllersTests

open NUnit.Framework
open FsUnitTyped
open System
open SwashbuckleReturnControllersTests

[<Test>]
let ``Update Bool GET Test`` () =
    api.GetApiUpdateBool(true)
    |> shouldEqual false

[<Test>]
let ``Update Bool POST Test`` () =
    api.PostApiUpdateBool(false)
    |> shouldEqual true


[<Test>]
let ``Update Int32 GET Test`` () =
    api.GetApiUpdateInt32(0)
    |> shouldEqual 1

[<Test>]
let ``Update Int32 POST Test`` () =
    api.PostApiUpdateInt32(0)
    |> shouldEqual 1


[<Test>]
let ``Update Int64 GET Test`` () =
    api.GetApiUpdateInt64(10L)
    |> shouldEqual 11L

[<Test>]
let ``Update Int64 POST Test`` () =
    api.PostApiUpdateInt64(10L)
    |> shouldEqual 11L


[<Test>]
let ``Update Float GET Test`` () =
    api.GetApiUpdateFloat(1.0f)
    |> shouldEqual 2.0f

[<Test>]
let ``Update Float POST Test`` () =
    api.PostApiUpdateFloat(1.0f)
    |> shouldEqual 2.0f


[<Test>]
let ``Update Double GET Test`` () =
    api.GetApiUpdateDouble(2.0)
    |> shouldEqual 3.0

[<Test>]
let ``Update Double POST Test`` () =
    api.PostApiUpdateDouble(2.0)
    |> shouldEqual 3.0


[<Test>]
let ``Update String GET Test`` () =
    api.GetApiUpdateString("Serge")
    |> shouldEqual "Hello, Serge"

[<Test>]
let ``Update String POST Test`` () =
    api.PostApiUpdateString("Serge")
    |> shouldEqual "Hello, Serge"


[<Test>]
let ``Update DateTime GET Test`` () =
    api.GetApiUpdateDateTime(DateTime(2015,1,1))
    |> shouldEqual (DateTime(2015,1,2))

[<Test>]
let ``Update DateTime POST Test`` () =
    api.PostApiUpdateDateTime(DateTime(2015,1,1))
    |> shouldEqual (DateTime(2015,1,2))


[<Test>]
let ``Update Enum GET Test`` () =
    api.GetApiUpdateEnum("1")
    |> shouldEqual "1"

[<Test>]
let ``Update Enum POST Test`` () =
    api.PostApiUpdateEnum("1")
    |> shouldEqual "1"


[<Test>]
let ``Update Array Int GET Test`` () =
    api.GetApiUpdateArrayInt([|3;2;1|])
    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Update Array Int POST Test`` () =
    api.PostApiUpdateArrayInt([|3;2;1|])
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Update Array Enum GET Test`` () =
    api.GetApiUpdateArrayEnum([|"2";"1"|])
    |> shouldEqual [|"1";"2"|]

[<Test>]
let ``Update Array Enum POST Test`` () =
    api.PostApiUpdateArrayEnum([|"2";"1"|])
    |> shouldEqual [|"1";"2"|]


//[<Test>] // "ExceptionMessage":"No parameterless constructor defined for this object."
//let ``Update List Int GET Test`` () =
//    WebAPI.UpdateListInt.Get([|3;2;1|])
//    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Update List Int POST Test`` () =
    api.PostApiUpdateListInt([|3;2;1|])
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Update Seq Int GET Test`` () =
    api.GetApiUpdateSeqInt([|3;2;1|])
    |> shouldEqual [|1;2;3|]

[<Test>]
let ``Update Seq Int POST Test`` () =
    api.PostApiUpdateSeqInt([|3;2;1|])
    |> shouldEqual [|1;2;3|]


[<Test>]
let ``Update Object Point GET Test`` () =
    let point = api.GetApiUpdateObjectPointClass(xX = Some 1, xY = Some 2)
    point.X |> shouldEqual (Some 2)
    point.Y |> shouldEqual (Some 1)

[<Test>]
let ``Update Object Point POST Test`` () =
    let p = WebAPI.PointClass()
    p.X <- Some 1
    p.Y <- Some 2
    let point = api.PostApiUpdateObjectPointClass(p)
    point.X |> shouldEqual (Some 2)
    point.Y |> shouldEqual (Some 1)
