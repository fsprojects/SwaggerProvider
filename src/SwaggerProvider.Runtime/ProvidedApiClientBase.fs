namespace Swagger

open System
open System.Net.Http
open System.Threading.Tasks
open System.Text.Json
open System.Text.Json.Serialization

type OpenApiException(code: int, description: string, headers: Headers.HttpResponseHeaders, content: HttpContent, ?responseBody: string) =
    inherit
        Exception(
            match responseBody with
            | Some body when not(String.IsNullOrEmpty(body)) -> sprintf "%s\nResponse body: %s" description body
            | _ -> description
        )

    member _.StatusCode = code
    member _.Description = description
    member _.Headers = headers
    member _.Content = content
    /// The raw response body returned by the server, if available.
    member _.ResponseBody = defaultArg responseBody ""

type ProvidedApiClientBase(httpClient: HttpClient, options: JsonSerializerOptions) =

#if TP_RUNTIME
    let options =
        if isNull options then
            let options = JsonSerializerOptions()
            options.Converters.Add(JsonFSharpConverter())
            options
        else
            options
#endif

    // Mutable field populated by CallAsync after each successful response.
    // Not thread-safe — concurrent calls may interleave; single-threaded sequential usage is typical.
    let mutable lastResponseHeadersValue: System.Collections.Generic.IReadOnlyDictionary<string, string> =
        System.Collections.Generic.Dictionary<string, string>() :> System.Collections.Generic.IReadOnlyDictionary<string, string>

    member val HttpClient = httpClient with get, set

    /// The HTTP response headers from the most recent successful API call made on this client.
    /// Includes both response headers and content headers.  Not safe for concurrent use — if
    /// multiple calls are made simultaneously the result is the headers from whichever completed
    /// last.
    member _.LastResponseHeaders: System.Collections.Generic.IReadOnlyDictionary<string, string> =
        lastResponseHeadersValue

    abstract member Serialize: obj -> string
    abstract member Deserialize: string * Type -> obj

    default _.Serialize(value: obj) : string =
        JsonSerializer.Serialize(value, options)

    default _.Deserialize(value, retTy: Type) : obj =
        JsonSerializer.Deserialize(value, retTy, options)

    member this.CallAsync
        (request: HttpRequestMessage, errorCodes: string[], errorDescriptions: string[], cancellationToken: System.Threading.CancellationToken)
        : Task<HttpResponseMessage> =
        task {
            let! response = this.HttpClient.SendAsync(request, cancellationToken)

            if response.IsSuccessStatusCode then
                // Collect response headers (both message headers and content headers) so that
                // LastResponseHeaders is populated for callers that need e.g. Location headers.
                let dict =
                    System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

                for kvp in response.Headers do
                    if not(dict.ContainsKey(kvp.Key)) then
                        dict[kvp.Key] <- Seq.head kvp.Value

                if not(isNull response.Content) then
                    for kvp in response.Content.Headers do
                        if not(dict.ContainsKey(kvp.Key)) then
                            dict[kvp.Key] <- Seq.head kvp.Value

                lastResponseHeadersValue <-
                    System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(dict)
                    :> System.Collections.Generic.IReadOnlyDictionary<string, string>

                return response
            else
                let code = response.StatusCode |> int
                let codeStr = code |> string

                let readBody() =
                    task {
                        try
#if NET5_0_OR_GREATER
                            return! response.Content.ReadAsStringAsync(cancellationToken)
#else
                            return! response.Content.ReadAsStringAsync()
#endif
                        with
                        | :? OperationCanceledException as e -> return raise e
                        | _ ->
                            // If reading the body fails (e.g., disposed stream or invalid charset),
                            // fall back to an empty body so we can still throw OpenApiException.
                            return ""
                    }

                match errorCodes |> Array.tryFindIndex((=) codeStr) with
                | Some idx ->
                    let desc = errorDescriptions[idx]
                    let! body = readBody()
                    return raise(OpenApiException(code, desc, response.Headers, response.Content, body))
                | None ->
                    let! body = readBody()

                    let desc =
                        if String.IsNullOrEmpty(response.ReasonPhrase) then
                            $"HTTP {code}"
                        else
                            response.ReasonPhrase

                    return raise(OpenApiException(code, desc, response.Headers, response.Content, body))
        }
