namespace Swagger.Serialization

open System
open Newtonsoft.Json
open Microsoft.FSharp.Reflection

#if TP_RUNTIME

/// Serializer for serializing the F# option types.
// https://github.com/eulerfx/JsonNet.FSharp
type OptionConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) =
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.WriteJson(writer, value, serializer) =
        let value =
            if isNull value then null
            else
                let _,fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, t, _, serializer) =
        let innerType = t.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then (typedefof<Nullable<_>>).MakeGenericType([|innerType|])
            else innerType
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(t)
        if isNull value then FSharpValue.MakeUnion(cases.[0], [||])
        else FSharpValue.MakeUnion(cases.[1], [|value|])

type ByteArrayConverter() =
    inherit JsonConverter()
    override __.CanConvert(t) = t = typeof<byte[]>
    override __.WriteJson(writer, value, serializer) =
        let bytes = value :?> byte[]
        let str = System.Convert.ToBase64String(bytes)
        serializer.Serialize(writer, str)
    override __.ReadJson(reader, _, _, serializer) =
        let value = serializer.Deserialize(reader, typeof<string>) :?> string
        Convert.FromBase64String(value) :> obj

#endif