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

    member val HttpClient = httpClient with get, set

    abstract member Serialize: obj -> string
    abstract member Deserialize: string * Type -> obj

    default _.Serialize(value: obj) : string =
        JsonSerializer.Serialize(value, options)

    default _.Deserialize(value, retTy: Type) : obj =
        JsonSerializer.Deserialize(value, retTy, options)

    member this.CallAsync(request: HttpRequestMessage, errorCodes: string[], errorDescriptions: string[]) : Task<HttpContent> =
        task {
            let! response = this.HttpClient.SendAsync(request)

            if response.IsSuccessStatusCode then
                return response.Content
            else
                let code = response.StatusCode |> int
                let codeStr = code |> string

                match errorCodes |> Array.tryFindIndex((=) codeStr) with
                | Some idx ->
                    let desc = errorDescriptions[idx]
                    let! body =
                        task {
                            try
                                return! response.Content.ReadAsStringAsync()
                            with _ ->
                                // If reading the body fails (e.g., disposed stream or invalid charset),
                                // fall back to an empty body so we can still throw OpenApiException.
                                return ""
                        }
                    return raise(OpenApiException(code, desc, response.Headers, response.Content, body))
                | None ->
                    // fail with HttpRequestException if we do not know error description
                    return response.EnsureSuccessStatusCode().Content
        }
