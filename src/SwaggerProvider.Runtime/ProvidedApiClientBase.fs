namespace Swagger

open System
open System.Net.Http
open Newtonsoft.Json

open Swagger.Serialization

type OpenApiException(code:int, description:string) =
    inherit Exception(description)
    member __.StatusCode = code
    member __.Description = description

type ProvidedApiClientBase(httpClient: HttpClient) =
    let jsonSerializerSettings =
        let settings =
            JsonSerializerSettings(
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None)
#if TP_RUNTIME
        [
            OptionConverter () :> JsonConverter
            ByteArrayConverter () :> JsonConverter
        ]
        |> List.iter settings.Converters.Add
#endif
        settings

    member val HttpClient = httpClient with get, set

    abstract member Serialize: obj -> string
    abstract member Deserialize: string * Type -> obj

    default __.Serialize(value:obj): string =
        JsonConvert.SerializeObject(value, jsonSerializerSettings)
    default __.Deserialize(value, retTy:Type): obj =
        JsonConvert.DeserializeObject(value, retTy, jsonSerializerSettings)

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
