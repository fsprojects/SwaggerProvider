namespace SwaggerProvider.Internal

open System
open Newtonsoft.Json
open Microsoft.FSharp.Reflection

type ProvidedSwaggerBaseType (host:string) =
    member val Host = host with get, set
    member val Headers = Array.empty<string*string> with get, set
    member val CustomizeHttpRequest = (id:Net.HttpWebRequest -> Net.HttpWebRequest) with get, set

/// Serializer for serializing the F# option types.
// https://github.com/eulerfx/JsonNet.FSharp
type private OptionConverter() =
    inherit JsonConverter()

    override x.CanConvert(t) =
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override x.WriteJson(writer, value, serializer) =
        let value =
            if isNull value then null
            else
                let _,fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]
        serializer.Serialize(writer, value)

    override x.ReadJson(reader, t, existingValue, serializer) =
        let innerType = t.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then (typedefof<Nullable<_>>).MakeGenericType([|innerType|])
            else innerType
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(t)
        if isNull value then FSharpValue.MakeUnion(cases.[0], [||])
        else FSharpValue.MakeUnion(cases.[1], [|value|])

type private ByteArrayConverter() =
    inherit JsonConverter()
    override __.CanConvert(t) = t = typeof<byte[]>
    override __.WriteJson(writer, value, serializer) =
        let bytes = value :?> byte[]
        let str = System.Convert.ToBase64String(bytes)
        serializer.Serialize(writer, str)
    override __.ReadJson(reader, t, existingValue, serializer) =
        let value = serializer.Deserialize(reader, typeof<string>) :?> string
        System.Convert.FromBase64String(value) :> obj


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
        | :? array<System.DateTime> as xs -> xs |> toStrArray name
        | :? array<Option<bool>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int64>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<float32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<double>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<string>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<System.DateTime>> as xs -> xs |> toStrArray name
        | :? Option<bool> as x -> x |> toStrOpt name
        | :? Option<int32> as x -> x |> toStrOpt name
        | :? Option<int64> as x -> x |> toStrOpt name
        | :? Option<float32> as x -> x |> toStrOpt name
        | :? Option<double> as x -> x |> toStrOpt name
        | :? Option<string> as x -> x |> toStrOpt name
        | :? Option<System.DateTime> as x -> x |> toStrOpt name
        | _ -> [name, obj.ToString()]

    let getPropertyNameAttribute name =
        { new Reflection.CustomAttributeData() with
            member __.Constructor =  typeof<Newtonsoft.Json.JsonPropertyAttribute>.GetConstructor([|typeof<string>|])
            member __.ConstructorArguments = [|Reflection.CustomAttributeTypedArgument(typeof<string>, name)|] :> System.Collections.Generic.IList<_>
            member __.NamedArguments = [||] :> System.Collections.Generic.IList<_> }

    let serialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
        settings.Converters.Add(new OptionConverter () :> JsonConverter)
        settings.Converters.Add(new ByteArrayConverter () :> JsonConverter)
        fun (value:obj) ->
            JsonConvert.SerializeObject(value, settings)

    let deserialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented)
        settings.Converters.Add(new OptionConverter () :> JsonConverter)
        settings.Converters.Add(new ByteArrayConverter () :> JsonConverter)
        fun value (retTy:Type) ->
            JsonConvert.DeserializeObject(value, retTy, settings)