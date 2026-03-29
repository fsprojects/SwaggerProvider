module Swashbuckle.v2.CancellationTokenTests

open Xunit
open FsUnitTyped
open System
open System.Net.Http
open System.Threading
open SwaggerProvider
open Swashbuckle.v2.ReturnControllersTests

type WebAPIAsync =
    SwaggerClientProvider<"http://localhost:5000/swagger/v1/swagger.json", IgnoreOperationId=true, SsrfProtection=false, PreferAsync=true>

let apiAsync =
    let handler = new HttpClientHandler(UseCookies = false)

    let client =
        new HttpClient(handler, true, BaseAddress = Uri("http://localhost:5000"))

    WebAPIAsync.Client(client)

[<Fact>]
let ``v2 Call generated method without CancellationToken uses default token``() =
    task {
        let! result = api.GetApiReturnBoolean()
        result |> shouldEqual true
    }

[<Fact>]
let ``v2 Call generated method with explicit CancellationToken None``() =
    task {
        let! result = api.GetApiReturnBoolean(CancellationToken.None)
        result |> shouldEqual true
    }

[<Fact>]
let ``v2 Call generated method with valid CancellationTokenSource token``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiReturnInt32(cts.Token)
        result |> shouldEqual 42
    }

[<Fact>]
let ``v2 Call generated method with already-cancelled token raises OperationCanceledException``() =
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
let ``v2 Call POST generated method with explicit CancellationToken None``() =
    task {
        let! result = api.PostApiReturnString(CancellationToken.None)
        result |> shouldEqual "Hello world"
    }

[<Fact>]
let ``v2 Call async generated method without CancellationToken uses default token``() =
    async {
        let! result = apiAsync.GetApiReturnBoolean()
        result |> shouldEqual true
    }
    |> Async.StartAsTask

[<Fact>]
let ``v2 Call method with required param and explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiUpdateString("Serge", cts.Token)
        result |> shouldEqual "Hello, Serge"
    }

[<Fact>]
let ``v2 Call method with optional param and explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiUpdateBool(Some true, cts.Token)
        result |> shouldEqual false
    }

[<Fact>]
let ``v2 Call async generated method with explicit CancellationToken``() =
    async {
        use cts = new CancellationTokenSource()
        let! result = apiAsync.GetApiReturnInt32(cts.Token)
        result |> shouldEqual 42
    }
    |> Async.StartAsTask

[<Fact>]
let ``v2 Call async generated method with already-cancelled token raises OperationCanceledException``() =
    async {
        use cts = new CancellationTokenSource()
        cts.Cancel()

        try
            let! _ = apiAsync.GetApiReturnString(cts.Token)
            failwith "Expected OperationCanceledException"
        with
        | :? OperationCanceledException -> ()
        | :? AggregateException as aex when (aex.InnerException :? OperationCanceledException) -> ()
    }
    |> Async.StartAsTask

[<Fact>]
let ``v2 Call async POST generated method with explicit CancellationToken``() =
    async {
        use cts = new CancellationTokenSource()
        let! result = apiAsync.PostApiReturnString(cts.Token)
        result |> shouldEqual "Hello world"
    }
    |> Async.StartAsTask
