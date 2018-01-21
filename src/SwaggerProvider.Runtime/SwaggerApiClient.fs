namespace Swagger

open System
open System.Net.Http
open System.Threading.Tasks
open Newtonsoft.Json

open Swagger.Serialization

type SwaggerException() = 
    inherit Exception()

type SwaggerApiClientBase(httpClient: HttpClient) =
    let jsonSerializerSettings = 
        let settings = 
            JsonSerializerSettings(
                NullValueHandling = NullValueHandling.Ignore, 
                Formatting = Formatting.Indented)
        [
            OptionConverter () :> JsonConverter
            ByteArrayConverter () :> JsonConverter
        ] 
        |> List.iter settings.Converters.Add
        settings
    
    abstract member Serialize: obj -> string
    abstract member Deserialize: string * Type -> obj
    abstract member CallAsync: HttpRequestMessage -> Task<HttpResponseMessage> 

    default __.Serialize(value:obj): string =
        JsonConvert.SerializeObject(value, jsonSerializerSettings)

    default __.Deserialize(value, retTy:Type): obj =
        JsonConvert.DeserializeObject(value, retTy, jsonSerializerSettings)

    default __.CallAsync(request: HttpRequestMessage) : Task<HttpResponseMessage> =
        httpClient.SendAsync(request)