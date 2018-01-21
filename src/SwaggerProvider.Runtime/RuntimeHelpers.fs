namespace Swagger.Internal

open System
open Newtonsoft.Json

open Swagger.Serialization

type ProvidedSwaggerBaseType (host:string) =
    member val Host = host with get, set
    member val Headers = Array.empty<string*string> with get, set
    member val CustomizeHttpRequest = (id:Net.HttpWebRequest -> Net.HttpWebRequest) with get, set

module RuntimeHelpers =

    let inline private toStrArray name values =
        values
        |> Array.map (fun value-> name, value.ToString())
        |> Array.toList

    let inline private toStrArrayOpt name values =
        values
        |> Array.choose (id)
        |> toStrArray name

    let inline private toStrOpt name value =
        match value with
        | Some(x) -> [name, x.ToString()]
        | None ->[]

    let toQueryParams (name:string) (obj:obj) =
        match obj with
        | :? array<byte> as xs -> xs |> toStrArray name
        | :? array<bool> as xs -> xs |> toStrArray name
        | :? array<int32> as xs -> xs |> toStrArray name
        | :? array<int64> as xs -> xs |> toStrArray name
        | :? array<float32> as xs -> xs |> toStrArray name
        | :? array<double> as xs -> xs |> toStrArray name
        | :? array<string> as xs -> xs |> toStrArray name
        | :? array<DateTime> as xs -> xs |> toStrArray name
        | :? array<Option<bool>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int64>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<float32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<double>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<string>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<DateTime>> as xs -> xs |> toStrArray name
        | :? Option<bool> as x -> x |> toStrOpt name
        | :? Option<int32> as x -> x |> toStrOpt name
        | :? Option<int64> as x -> x |> toStrOpt name
        | :? Option<float32> as x -> x |> toStrOpt name
        | :? Option<double> as x -> x |> toStrOpt name
        | :? Option<string> as x -> x |> toStrOpt name
        | :? Option<DateTime> as x -> x |> toStrOpt name
        | _ -> [name, obj.ToString()]

    let getPropertyNameAttribute name =
        { new Reflection.CustomAttributeData() with
            member __.Constructor =  typeof<JsonPropertyAttribute>.GetConstructor([|typeof<string>|])
            member __.ConstructorArguments = [|Reflection.CustomAttributeTypedArgument(typeof<string>, name)|] :> Collections.Generic.IList<_>
            member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

    let serialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
        settings.Converters.Add(OptionConverter () :> JsonConverter)
        settings.Converters.Add(ByteArrayConverter () :> JsonConverter)
        fun (value:obj) ->
            JsonConvert.SerializeObject(value, settings)

    let deserialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented)
        settings.Converters.Add(OptionConverter () :> JsonConverter)
        settings.Converters.Add(ByteArrayConverter () :> JsonConverter)
        fun value (retTy:Type) ->
            JsonConvert.DeserializeObject(value, retTy, settings)

    let combineUrl (urlA:string) (urlB:string) =
        sprintf "%s/%s" (urlA.TrimEnd('/')) (urlB.TrimStart('/'))
