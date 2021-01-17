namespace Swagger

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization

type OpenApiException(code:int, description:string) =
    inherit Exception(description)
    member __.StatusCode = code
    member __.Description = description

type ProvidedApiClientBase(httpClient: HttpClient, options: JsonSerializerOptions) =

#if TP_RUNTIME
    let options =
        if isNull options then
            let options = JsonSerializerOptions()
            [
                JsonFSharpConverter(
                    JsonUnionEncoding.InternalTag
                    ||| JsonUnionEncoding.NamedFields
                    ||| JsonUnionEncoding.UnwrapSingleCaseUnions
                    ||| JsonUnionEncoding.UnwrapRecordCases
                    ||| JsonUnionEncoding.UnwrapOption) :> JsonConverter
            ]
            |> List.iter options.Converters.Add
            options
        else options
#endif

    member val HttpClient = httpClient with get, set

    abstract member Serialize: obj -> string
    abstract member Deserialize: string * Type -> obj

    default __.Serialize(value:obj): string =
        JsonSerializer.Serialize(value, options)
    default __.Deserialize(value, retTy:Type): obj =
        JsonSerializer.Deserialize(value, retTy, options)

    // This code may change in the future, especially when task{} become part of FSharp.Core.dll
    member this.CallAsync(request: HttpRequestMessage, errorCodes:string[], errorDescriptions:string[]) : Async<HttpContent> =
        async {
            let! response = this.HttpClient.SendAsync(request) |> Async.AwaitTask
            if response.IsSuccessStatusCode
            then return response.Content
            else
                let code = response.StatusCode |> int
                let codeStr = code |> string
                errorCodes
                |> Array.tryFindIndex((=)codeStr)
                |> Option.iter (fun idx ->
                    let desc = errorDescriptions.[idx]
                    raise (OpenApiException(code, desc)))

                // fail with HttpRequestException if we do not know error description
                return response.EnsureSuccessStatusCode().Content
        }
