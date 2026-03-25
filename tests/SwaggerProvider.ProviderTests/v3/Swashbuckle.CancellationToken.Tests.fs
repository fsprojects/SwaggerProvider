module Swashbuckle.v3.CancellationTokenTests

open Xunit
open FsUnitTyped
open System
open System.Net.Http
open System.Threading
open SwaggerProvider
open Swashbuckle.v3.ReturnControllersTests

type WebAPIAsync =
    OpenApiClientProvider<"http://localhost:5000/swagger/v1/openapi.json", IgnoreOperationId=true, SsrfProtection=false, PreferAsync=true>

let apiAsync =
    let handler = new HttpClientHandler(UseCookies = false)

    let client =
        new HttpClient(handler, true, BaseAddress = Uri("http://localhost:5000"))

    WebAPIAsync.Client(client)

[<Fact>]
let ``Call generated method without CancellationToken uses default token``() =
    task {
        let! result = api.GetApiReturnBoolean()
        result |> shouldEqual true
    }

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

[<Fact>]
let ``Call async generated method without CancellationToken uses default token``() =
    async {
        let! result = apiAsync.GetApiReturnBoolean()
        result |> shouldEqual true
    }

[<Fact>]
let ``Call method with required param and explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiUpdateString("Serge", cts.Token)
        result |> shouldEqual "Hello, Serge"
    }

[<Fact>]
let ``Call method with optional param and explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiUpdateBool(Some true, cts.Token)
        result |> shouldEqual false
    }

[<Fact>]
let ``Call async generated method with explicit CancellationToken``() =
    async {
        use cts = new CancellationTokenSource()
        let! result = apiAsync.GetApiReturnInt32(cts.Token)
        result |> shouldEqual 42
    }

[<Fact>]
let ``Call stream-returning method with explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiReturnFile(cts.Token)
        use reader = new IO.StreamReader(result)
        let! content = reader.ReadToEndAsync()
        content |> shouldEqual "I am totally a file's\ncontent"
    }

[<Fact>]
let ``Call text-returning method with explicit CancellationToken``() =
    task {
        use cts = new CancellationTokenSource()
        let! result = api.GetApiReturnPlain(cts.Token)
        result |> shouldEqual "Hello world"
    }

[<Fact>]
let ``Call async generated method with already-cancelled token raises OperationCanceledException``() =
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

[<Fact>]
let ``Call async POST generated method with explicit CancellationToken``() =
    async {
        use cts = new CancellationTokenSource()
        let! result = apiAsync.PostApiReturnString(cts.Token)
        result |> shouldEqual "Hello world"
    }
