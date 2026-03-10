namespace SwaggerProvider.Tests.RuntimeHelpersTests

open System
open System.IO
open System.Net.Http
open System.Text.Json
open Xunit
open FsUnitTyped
open Swagger.Internal.RuntimeHelpers

/// Unit tests for RuntimeHelpers — the runtime parameter serialization and HTTP utilities.
/// These functions are used by every generated API client but previously had no dedicated tests.
module ToParamTests =

    [<Fact>]
    let ``toParam formats DateTime as ISO 8601 round-trip``() =
        let dt = DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)
        let result = toParam(box dt)
        result |> shouldEqual(dt.ToString("O"))

    [<Fact>]
    let ``toParam formats DateTimeOffset as ISO 8601 round-trip``() =
        let dto = DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(2.0))
        let result = toParam(box dto)
        result |> shouldEqual(dto.ToString("O"))

    [<Fact>]
    let ``toParam returns null for null input``() =
        let result = toParam null
        result |> shouldEqual null

    [<Fact>]
    let ``toParam uses ToString for integers``() =
        let result = toParam(box 42)
        result |> shouldEqual "42"

    [<Fact>]
    let ``toParam uses ToString for strings``() =
        let result = toParam(box "hello world")
        result |> shouldEqual "hello world"

    [<Fact>]
    let ``toParam uses ToString for Guid``() =
        let g = Guid("d3b07384-d9a2-4e3f-9a4b-1234567890ab")
        let result = toParam(box g)
        result |> shouldEqual(g.ToString())


module ToQueryParamsTests =

    let private stubClient =
        { new Swagger.ProvidedApiClientBase(null, JsonSerializerOptions()) with
            override _.Serialize(v) =
                JsonSerializer.Serialize v

            override _.Deserialize(s, t) =
                JsonSerializer.Deserialize(s, t) }

    [<Fact>]
    let ``toQueryParams handles string array``() =
        let result = toQueryParams "tag" (box [| "alpha"; "beta"; "gamma" |]) stubClient

        result
        |> shouldEqual [ ("tag", "alpha"); ("tag", "beta"); ("tag", "gamma") ]

    [<Fact>]
    let ``toQueryParams handles int32 array``() =
        let result = toQueryParams "id" (box [| 1; 2; 3 |]) stubClient
        result |> shouldEqual [ ("id", "1"); ("id", "2"); ("id", "3") ]

    [<Fact>]
    let ``toQueryParams handles int64 array``() =
        let result = toQueryParams "id" (box [| 1L; 2L; 3L |]) stubClient
        result |> shouldEqual [ ("id", "1"); ("id", "2"); ("id", "3") ]

    [<Fact>]
    let ``toQueryParams handles bool array``() =
        let result = toQueryParams "flag" (box [| true; false |]) stubClient
        result |> shouldEqual [ ("flag", "True"); ("flag", "False") ]

    [<Fact>]
    let ``toQueryParams formats DateTime array as ISO 8601``() =
        let dt = DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)
        let result = toQueryParams "d" (box [| dt |]) stubClient
        result |> shouldHaveLength 1
        fst result[0] |> shouldEqual "d"
        snd result[0] |> shouldEqual(dt.ToString("O"))

    [<Fact>]
    let ``toQueryParams formats DateTimeOffset array as ISO 8601``() =
        let dto = DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero)
        let result = toQueryParams "d" (box [| dto |]) stubClient
        result |> shouldHaveLength 1
        snd result[0] |> shouldEqual(dto.ToString("O"))

    [<Fact>]
    let ``toQueryParams handles Option<string> Some``() =
        let result = toQueryParams "q" (box(Some "hello")) stubClient
        result |> shouldEqual [ ("q", "hello") ]

    [<Fact>]
    let ``toQueryParams handles Option<string> None``() =
        let result = toQueryParams "q" (box(Option<string>.None)) stubClient
        result |> shouldEqual []

    [<Fact>]
    let ``toQueryParams handles Option<int32> Some``() =
        let result = toQueryParams "n" (box(Some 99)) stubClient
        result |> shouldEqual [ ("n", "99") ]

    [<Fact>]
    let ``toQueryParams handles Option<int32> None``() =
        let result = toQueryParams "n" (box(Option<int>.None)) stubClient
        result |> shouldEqual []

    [<Fact>]
    let ``toQueryParams handles Option<DateTime> Some as ISO 8601``() =
        let dt = DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        let result = toQueryParams "d" (box(Some dt)) stubClient
        result |> shouldHaveLength 1
        snd result[0] |> shouldEqual(dt.ToString("O"))

    [<Fact>]
    let ``toQueryParams handles Option<DateTime> None``() =
        let result = toQueryParams "d" (box(Option<DateTime>.None)) stubClient
        result |> shouldEqual []

    [<Fact>]
    let ``toQueryParams handles Option<Guid> Some``() =
        let g = Guid.NewGuid()
        let result = toQueryParams "id" (box(Some g)) stubClient
        result |> shouldEqual [ ("id", g.ToString()) ]

    [<Fact>]
    let ``toQueryParams handles plain DateTime as ISO 8601``() =
        let dt = DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc)
        let result = toQueryParams "dt" (box dt) stubClient
        result |> shouldHaveLength 1
        snd result[0] |> shouldEqual(dt.ToString("O"))

    [<Fact>]
    let ``toQueryParams handles plain DateTimeOffset as ISO 8601``() =
        let dto = DateTimeOffset(2024, 6, 1, 8, 0, 0, TimeSpan.FromHours(-5.0))
        let result = toQueryParams "dto" (box dto) stubClient
        result |> shouldHaveLength 1
        snd result[0] |> shouldEqual(dto.ToString("O"))

    [<Fact>]
    let ``toQueryParams handles plain string``() =
        let result = toQueryParams "q" (box "search term") stubClient
        result |> shouldEqual [ ("q", "search term") ]

    [<Fact>]
    let ``toQueryParams returns empty list for null input (treated as Option None)``() =
        // In F#, None for reference option types is compiled as null at the .NET level,
        // so a null obj matches Option<string> None and returns an empty list.
        let result = toQueryParams "q" null stubClient
        result |> shouldEqual []

    [<Fact>]
    let ``toQueryParams skips None items in Option array``() =
        let values: Option<int>[] = [| Some 1; None; Some 3 |]
        let result = toQueryParams "n" (box values) stubClient
        result |> shouldEqual [ ("n", "1"); ("n", "3") ]

    [<Fact>]
    let ``toQueryParams handles Guid array``() =
        let g1 = Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        let g2 = Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        let result = toQueryParams "id" (box [| g1; g2 |]) stubClient
        result |> shouldEqual [ ("id", g1.ToString()); ("id", g2.ToString()) ]

    [<Fact>]
    let ``toQueryParams handles float32 array``() =
        let result = toQueryParams "v" (box [| 1.5f; 2.5f |]) stubClient
        result |> shouldEqual [ ("v", "1.5"); ("v", "2.5") ]

    [<Fact>]
    let ``toQueryParams handles double array``() =
        let result = toQueryParams "v" (box [| 1.5; 2.5 |]) stubClient
        result |> shouldEqual [ ("v", "1.5"); ("v", "2.5") ]

    [<Fact>]
    let ``toQueryParams handles byte array as base64``() =
        // byte[] is serialized via client.Serialize (JSON base64) with surrounding quotes trimmed
        let bytes = [| 72uy; 101uy; 108uy; 108uy; 111uy |] // "Hello" in ASCII
        let expected = (JsonSerializer.Serialize bytes).Trim('"')
        let result = toQueryParams "data" (box bytes) stubClient
        result |> shouldEqual [ ("data", expected) ]

    [<Fact>]
    let ``toQueryParams skips None items in Option<string> array``() =
        let values: Option<string>[] = [| Some "a"; None; Some "c" |]
        let result = toQueryParams "q" (box values) stubClient
        result |> shouldEqual [ ("q", "a"); ("q", "c") ]

    [<Fact>]
    let ``toQueryParams skips None items in Option<float32> array``() =
        let values: Option<float32>[] = [| Some 1.5f; None; Some 3.5f |]
        let result = toQueryParams "v" (box values) stubClient
        result |> shouldEqual [ ("v", "1.5"); ("v", "3.5") ]

    [<Fact>]
    let ``toQueryParams skips None items in Option<double> array``() =
        let values: Option<double>[] = [| Some 1.5; None; Some 3.5 |]
        let result = toQueryParams "v" (box values) stubClient
        result |> shouldEqual [ ("v", "1.5"); ("v", "3.5") ]


module CombineUrlTests =

    [<Fact>]
    let ``combineUrl joins paths without extra slashes``() =
        combineUrl "http://example.com/api" "v1/users"
        |> shouldEqual "http://example.com/api/v1/users"

    [<Fact>]
    let ``combineUrl trims trailing slash from left``() =
        combineUrl "http://example.com/api/" "v1/users"
        |> shouldEqual "http://example.com/api/v1/users"

    [<Fact>]
    let ``combineUrl trims leading slash from right``() =
        combineUrl "http://example.com/api" "/v1/users"
        |> shouldEqual "http://example.com/api/v1/users"

    [<Fact>]
    let ``combineUrl trims both slashes``() =
        combineUrl "http://example.com/api/" "/v1/users"
        |> shouldEqual "http://example.com/api/v1/users"

    [<Fact>]
    let ``combineUrl works with empty path segment``() =
        combineUrl "http://example.com" ""
        |> shouldEqual "http://example.com/"


module CreateHttpRequestTests =

    [<Fact>]
    let ``createHttpRequest creates GET request``() =
        use req = createHttpRequest "GET" "v1/users" []
        req.Method |> shouldEqual HttpMethod.Get

    [<Fact>]
    let ``createHttpRequest creates POST request``() =
        use req = createHttpRequest "POST" "v1/users" []
        req.Method |> shouldEqual HttpMethod.Post

    [<Fact>]
    let ``createHttpRequest creates DELETE request``() =
        use req = createHttpRequest "DELETE" "v1/users/42" []
        req.Method |> shouldEqual HttpMethod.Delete

    [<Fact>]
    let ``createHttpRequest is case-insensitive for method``() =
        use req = createHttpRequest "get" "v1/users" []
        req.Method |> shouldEqual HttpMethod.Get

    [<Fact>]
    let ``createHttpRequest appends query parameters``() =
        use req = createHttpRequest "GET" "v1/users" [ ("page", "2"); ("size", "10") ]
        let uri = req.RequestUri.ToString()
        uri |> shouldContainText "page=2"
        uri |> shouldContainText "size=10"

    [<Fact>]
    let ``createHttpRequest skips null query parameter values``() =
        use req = createHttpRequest "GET" "v1/users" [ ("q", null) ]
        let uri = req.RequestUri.ToString()
        uri |> shouldNotContainText "q="

    [<Fact>]
    let ``createHttpRequest includes path in request URI``() =
        use req = createHttpRequest "GET" "v1/pets/42" []
        req.RequestUri.ToString() |> shouldContainText "v1/pets/42"


module FillHeadersTests =

    [<Fact>]
    let ``fillHeaders adds standard headers``() =
        use req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/")
        fillHeaders req [ ("Accept", "application/json"); ("X-Api-Key", "secret") ]
        req.Headers.Contains("Accept") |> shouldEqual true
        req.Headers.Contains("X-Api-Key") |> shouldEqual true

    [<Fact>]
    let ``fillHeaders skips null-value headers``() =
        use req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/")
        fillHeaders req [ ("X-Missing", null) ]
        req.Headers.Contains("X-Missing") |> shouldEqual false

    [<Fact>]
    let ``fillHeaders silently ignores Content-Type header``() =
        // Content-Type must be set on HttpContent, not on the request; this should not throw
        use req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/")
        // Should not raise an exception even though Content-Type cannot be added to request headers
        fillHeaders req [ ("Content-Type", "application/json") ]


module ToContentTests =

    [<Fact>]
    let ``toStringContent returns JSON content type``() =
        use c = toStringContent "{\"key\":\"value\"}"
        c.Headers.ContentType.MediaType |> shouldEqual "application/json"

    [<Fact>]
    let ``toStringContent preserves the body``() =
        let body = "{\"name\":\"Alice\"}"
        use c = toStringContent body
        let text = c.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
        text |> shouldEqual body

    [<Fact>]
    let ``toTextContent returns text/plain content type``() =
        use c = toTextContent "hello"
        c.Headers.ContentType.MediaType |> shouldEqual "text/plain"

    [<Fact>]
    let ``toStreamContent sets provided content type``() =
        use stream = new MemoryStream([| 1uy; 2uy; 3uy |])
        use c = toStreamContent(box stream, "application/octet-stream")

        c.Headers.ContentType.MediaType
        |> shouldEqual "application/octet-stream"

    [<Fact>]
    let ``toStreamContent omits content type when empty``() =
        use stream = new MemoryStream([| 1uy; 2uy |])
        use c = toStreamContent(box stream, "")
        c.Headers.ContentType |> shouldEqual null

    [<Fact>]
    let ``toStreamContent throws for non-stream input``() =
        Assert.Throws<Exception>(fun () -> toStreamContent(box "not a stream", "application/json") |> ignore)
        |> ignore
