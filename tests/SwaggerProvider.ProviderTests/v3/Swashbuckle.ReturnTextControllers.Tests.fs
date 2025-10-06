module Swashbuckle.v3.ReturnTextControllersTests

open Xunit
open FsUnitTyped
open SwaggerProvider
open System
open System.Net.Http

type CallLoggingHandler(messageHandler) =
    inherit DelegatingHandler(messageHandler)

    override _.SendAsync(request, cancellationToken) =
        printfn $"[SendAsync]: %A{request.RequestUri}"
        base.SendAsync(request, cancellationToken)

type WebAPI = OpenApiClientProvider<"http://localhost:5000/swagger/v1/openapi.json", IgnoreOperationId=true>

let api =
    let handler = new HttpClientHandler(UseCookies = false)

    let client =
        new HttpClient(handler, true, BaseAddress = Uri("http://localhost:5000"))

    WebAPI.Client(client)

let asyncEqual expected actualTask =
    task {
        let! actual = actualTask
        actual |> shouldEqual expected
    }

[<Fact>]
let ``Return text/plain GET Test``() =
    api.GetApiReturnPlain() |> asyncEqual "Hello world"

[<Fact>]
let ``Return text/csv GET Test``() =
    api.GetApiReturnCsv() |> asyncEqual "Hello,world"
