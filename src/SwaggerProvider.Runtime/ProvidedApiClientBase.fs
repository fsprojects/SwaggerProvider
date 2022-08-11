namespace Swagger

open System
open System.Net.Http
open System.Threading.Tasks
open System.Text.Json
open System.Text.Json.Serialization

type OpenApiException(code: int, description: string) =
    inherit Exception(description)
    member __.StatusCode = code
    member __.Description = description

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

    default __.Serialize(value: obj) : string =
        JsonSerializer.Serialize(value, options)

    default __.Deserialize(value, retTy: Type) : obj =
        JsonSerializer.Deserialize(value, retTy, options)

    member this.CallAsync(request: HttpRequestMessage, errorCodes: string[], errorDescriptions: string[]) : Task<HttpContent> = task {
        let! response = this.HttpClient.SendAsync(request)

        if response.IsSuccessStatusCode then
            return response.Content
        else
            let code = response.StatusCode |> int
            let codeStr = code |> string

            errorCodes
            |> Array.tryFindIndex((=) codeStr)
            |> Option.iter(fun idx ->
                let desc = errorDescriptions.[idx]
                raise(OpenApiException(code, desc)))

            // fail with HttpRequestException if we do not know error description
            return response.EnsureSuccessStatusCode().Content
    }
