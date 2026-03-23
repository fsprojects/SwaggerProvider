module Swashbuckle.v3.CancellationTokenTests

open Xunit
open FsUnitTyped
open System
open System.Threading
open Swashbuckle.v3.ReturnControllersTests

[<Fact>]
let ``Call generated method with explicit CancellationToken None``() =
    task {
        let! result = api.GetApiReturnBoolean(CancellationToken.None)
        result |> shouldEqual true
    }

[<Fact>]
let ``Call generated method with valid CancellationTokenSource token``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiReturnInt32(cts.Token)
        result |> shouldEqual 42
    }

[<Fact>]
let ``Call generated method with already-cancelled token raises OperationCanceledException``() =
    task {
        use cts = new CancellationTokenSource()
        cts.Cancel()

        try
            let! _ = api.GetApiReturnString(cts.Token)
            failwith "Expected OperationCanceledException"
        with
        | :? OperationCanceledException -> ()
        | :? System.AggregateException as aex when (aex.InnerException :? OperationCanceledException) -> ()
    }

[<Fact>]
let ``Call POST generated method with explicit CancellationToken None``() =
    task {
        let! result = api.PostApiReturnString(CancellationToken.None)
        result |> shouldEqual "Hello world"
    }
