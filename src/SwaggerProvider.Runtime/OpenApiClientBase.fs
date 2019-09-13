namespace Swagger

open System
open System.Net.Http
open System.Threading.Tasks
open Newtonsoft.Json

open Swagger.Serialization

type OpenApiClientBase(httpClient: HttpClient) =
    let jsonSerializerSettings = 
        let settings = 
            JsonSerializerSettings(
                NullValueHandling = NullValueHandling.Ignore, 
                Formatting = Formatting.Indented)
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
    // TODO: CallAsync should return Task<HttpResponseMessage>
    abstract member CallAsync: HttpRequestMessage -> Async<string> 

    default __.Serialize(value:obj): string =
        JsonConvert.SerializeObject(value, jsonSerializerSettings)

    default __.Deserialize(value, retTy:Type): obj =
        JsonConvert.DeserializeObject(value, retTy, jsonSerializerSettings)

    default this.CallAsync(request: HttpRequestMessage) : Async<string> =
        async {
            let! response = this.HttpClient.SendAsync(request) |> Async.AwaitTask
            return! response.EnsureSuccessStatusCode().Content.ReadAsStringAsync() |> Async.AwaitTask
        }