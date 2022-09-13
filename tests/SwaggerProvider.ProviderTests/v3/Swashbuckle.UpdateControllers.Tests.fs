module Swashbuckle.v3.UpdateControllersTests

open Xunit
open FsUnitTyped
open System
open Swashbuckle.v3.ReturnControllersTests

let guid = Guid.NewGuid()
let guid2 = Guid.NewGuid()
let guid3 = Guid.NewGuid()


[<Fact>]
let ``Update Bool GET Test`` () =
    api.GetApiUpdateBool(Some true) |> asyncEqual false

[<Fact>]
let ``Update Bool POST Test`` () =
    api.PostApiUpdateBool(Some false) |> asyncEqual true


[<Fact>]
let ``Update Int32 GET Test`` () =
    api.GetApiUpdateInt32(Some 0) |> asyncEqual 1

[<Fact>]
let ``Update Int32 POST Test`` () =
    api.PostApiUpdateInt32(Some 0) |> asyncEqual 1


[<Fact>]
let ``Update Int64 GET Test`` () =
    api.GetApiUpdateInt64(Some 10L) |> asyncEqual 11L

[<Fact>]
let ``Update Int64 POST Test`` () =
    api.PostApiUpdateInt64(Some 10L) |> asyncEqual 11L

[<Fact>]
let ``Update Float GET Test`` () =
    api.GetApiUpdateFloat(Some 1.0f) |> asyncEqual 2.0f

[<Fact>]
let ``Update Float POST Test`` () =
    api.PostApiUpdateFloat(Some 1.0f) |> asyncEqual 2.0f


[<Fact>]
let ``Update Double GET Test`` () =
    api.GetApiUpdateDouble(Some 2.0) |> asyncEqual 3.0

[<Fact>]
let ``Update Double POST Test`` () =
    api.PostApiUpdateDouble(Some 2.0) |> asyncEqual 3.0


[<Fact>]
let ``Update String GET Test`` () =
    api.GetApiUpdateString("Serge") |> asyncEqual "Hello, Serge"

[<Fact>]
let ``Update String POST Test`` () =
    api.PostApiUpdateString("Serge") |> asyncEqual "Hello, Serge"


[<Fact>]
let ``Update DateTime GET Test`` () =
    api.GetApiUpdateDateTime(Some(DateTimeOffset <| DateTime(2015, 1, 1)))
    |> asyncEqual(DateTimeOffset <| DateTime(2015, 1, 2))

[<Fact>]
let ``Update DateTime POST Test`` () =
    api.PostApiUpdateDateTime(Some(DateTimeOffset <| DateTime(2015, 1, 1)))
    |> asyncEqual(DateTimeOffset <| DateTime(2015, 1, 2))

[<Fact>]
let ``Update Guid GET Test`` () =
    api.GetApiUpdateGuid(Some(guid)) |> asyncEqual guid

[<Fact>]
let ``Update Guid POST Test`` () =
    api.PostApiUpdateGuid(Some(guid)) |> asyncEqual guid

[<Fact>]
let ``Update Enum GET Test`` () =
    api.GetApiUpdateEnum("Absolute") |> asyncEqual "Absolute"

[<Fact>]
let ``Update Enum POST Test`` () =
    api.PostApiUpdateEnum("Absolute") |> asyncEqual "Absolute"


[<Fact>]
let ``Update Array Int GET Test`` () =
    api.GetApiUpdateArrayInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |]

[<Fact>]
let ``Update Array Int POST Test`` () =
    api.PostApiUpdateArrayInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |]


[<Fact>]
let ``Update Array Enum GET Test`` () =
    api.GetApiUpdateArrayEnum([| "Relative"; "Absolute" |])
    |> asyncEqual [| "Absolute"; "Relative" |]

[<Fact>]
let ``Update Array Enum POST Test`` () =
    api.PostApiUpdateArrayEnum([| "Relative"; "Absolute" |])
    |> asyncEqual [| "Absolute"; "Relative" |]

[<Fact>]
let ``Update Array Guid GET Test`` () =
    api.GetApiUpdateArrayGuid([| guid; guid2; guid3 |])
    |> asyncEqual [| guid3; guid2; guid |]

[<Fact>]
let ``Update Array Guid POST Test`` () =
    api.PostApiUpdateArrayGuid([| guid; guid2; guid3 |])
    |> asyncEqual [| guid3; guid2; guid |]

//TODO: System.InvalidOperationException: Could not create an instance of type 'Microsoft.FSharp.Collections.FSharpList`1[[System.Int32, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]'. Model bound complex types must not be abstract or value types and must have a parameterless constructor. Alternatively, give the 'x' parameter a non-null default value.
// testCaseAsync "Update List Int GET Test" <|
//     (api.GetApiUpdateListInt([|3;2;1|])
//      |> asyncEqual [|1;2;3|])

[<Fact>]
let ``Update List Int POST Test`` () =
    api.PostApiUpdateListInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |]


[<Fact>]
let ``Update Seq Int GET Test`` () =
    api.GetApiUpdateSeqInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |]

[<Fact>]
let ``Update Seq Int POST Test`` () =
    api.PostApiUpdateSeqInt([| 3; 2; 1 |]) |> asyncEqual [| 1; 2; 3 |])


// TODO: Server return point (0,0)
// testCaseAsync "Update Object Point GET Test" <| async {
//     let! point = api.GetApiUpdateObjectPointClass(x = Some 1, y = Some 2)
//     point.X |> shouldEqual (Some 2)
//     point.Y |> shouldEqual (Some 1)
// }

[<Fact>]
let ``Update Object Point POST Test`` () =
    task  {
        let p = WebAPI.PointClass()
        p.X <- Some 1
        p.Y <- Some 2
        let! point = api.PostApiUpdateObjectPointClass(p)
        point.X |> shouldEqual(Some 2)
        point.Y |> shouldEqual(Some 1)
    }

[<Fact>]
let ``Send and Receive object with byte[]`` () =
    task  {
        let x = WebAPI.FileDescription(Name = "2.txt", Bytes = [| 42uy |])
        let! y = api.PostApiUpdateObjectFileDescriptionClass(x)
        x.Name |> shouldEqual y.Name
        x.Bytes |> shouldEqual y.Bytes
    }

// System.Net.Http.HttpRequestException: Response status code does not indicate success: 400 (Bad Request).
[<Fact>]
let ``Send byte[] in query`` () =
    task {
        let bytes = api.Deserialize("\"NDI0Mg==\"", typeof<byte[]>) :?> byte[] // base64 encoded "4242"
        let! y = api.GetApiUpdateObjectFileDescriptionClass(bytes)
        y.Bytes |> shouldEqual(bytes)
    }

[<Fact>]
let ``Use Optional param Int`` () =
    task  {
        do! api.GetApiUpdateWithOptionalInt(Some 1) |> asyncEqual 2
        do! api.GetApiUpdateWithOptionalInt(Some 1, Some 2) |> asyncEqual 3
    }
