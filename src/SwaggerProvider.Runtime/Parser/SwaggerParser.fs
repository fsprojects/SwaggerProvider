namespace Swagger.Parser
open Swagger.Parser.Schema

module internal JsonAdapter =
    open Swagger.Parser.Exceptions
    open Newtonsoft.Json.Linq

    /// Schema node for Swagger schemes in Json format
    type JsonNodeAdapter(value:JToken) =
        inherit SchemaNode()

        override __.AsBoolean() = value.ToObject<bool>()

        override __.AsString() = value.ToObject<string>()

        override __.AsArray() =
            match value.Type with
            | JTokenType.Array -> 
                value :?> JArray
                |> Seq.map (fun x -> JsonNodeAdapter(x) :> SchemaNode)
                |> Seq.toArray
            | _ -> raise <| UnexpectedValueTypeException(value, "string")

        override __.AsStringArrayWithoutNull() =
            match value.Type with
            | JTokenType.String -> 
                [|value.ToObject<string>()|]
            | JTokenType.Array ->
                value :?> JArray
                |> Seq.map (fun x -> x.ToObject<string>())
                |> Seq.filter (fun x -> x <> "null")
                |> Seq.toArray
            | other -> 
                failwithf "Value: '%A' cannot be converted to StringArray" other

        override __.Properties() =
            match value.Type with
            | JTokenType.Object -> 
                (value :?> JObject).Properties()
                |> Seq.map (fun x -> x.Name, JsonNodeAdapter(x.Value) :> SchemaNode)
                |> Seq.toArray
            | _ -> raise <| UnexpectedValueTypeException(value, "JObject")

        override __.TryGetProperty(property) =
            match value.Type with
            | JTokenType.Object -> 
                let obj = value :?> JObject
                match obj.TryGetValue(property) with
                | true, x -> Some(JsonNodeAdapter(x) :> SchemaNode)
                | _ -> None
            | _ -> None

    let parse = JToken.Parse >> JsonNodeAdapter

module internal YamlAdapter =
    open Swagger.Parser.Exceptions
    open System.IO
    open YamlDotNet.Serialization
    open System.Collections.Generic

    // TODO: Merge this DU and Parse into YamlNodeAdapter
    type YamlNode =
        | Scalar of string
        | List of YamlNode list
        | Map of (string * YamlNode) list

    let parseYaml : (string -> YamlNode) =
        let rec loop (n: obj) =
            match n with
            | :? List<obj> as l -> YamlNode.List (l |> Seq.map loop |> Seq.toList)
            | :? Dictionary<obj,obj> as m ->
                Map (m |> Seq.choose (fun p ->
                    match p.Key with
                    | :? string as key -> Some (key, loop p.Value)
                    | _ -> None) |> Seq.toList)
            | scalar ->
                let value = if isNull scalar then "" else scalar.ToString()
                Scalar (value)

        let deserializer = Deserializer()
        fun (text:string) ->
            try
                use reader = new StringReader(text)
                deserializer.Deserialize(reader) |> loop
            with
            | :? YamlDotNet.Core.YamlException as e when not <| isNull e.InnerException ->
                raise e.InnerException // inner exceptions are much more informative
            | _ -> reraise()

    /// SchemaNode for Swagger schemes in Yaml format
    type YamlNodeAdapter(value:YamlNode) =
        inherit SchemaNode()

        override __.AsBoolean() =
            match value with
            | Scalar(x) -> System.Boolean.Parse(x)
            | _ -> raise <| UnexpectedValueTypeException(value, "bool")

        override __.AsString() =
            match value with
            | Scalar(x) -> x
            | _ -> raise <| UnexpectedValueTypeException(value, "string")

        override __.AsArray() =
            match value with
            | List(nodes) ->
                nodes |> List.map(fun x->YamlNodeAdapter(x) :> SchemaNode) |> Array.ofList
            | _ -> [||]

        override __.AsStringArrayWithoutNull() =
            match value with
            | Scalar(x) -> [|x|]
            | List(nodes) ->
                nodes |> Array.ofList
                |> Array.map(function
                    | Scalar (x) -> x
                    | x -> failwithf "'%A' cannot be converted to string" x)
                |> Array.filter (fun x -> x <> "null")
            | other -> failwithf "Value: '%A' cannot be converted to StringArray" other

        override __.Properties() =
            match value with
            | Map(pairs) -> pairs |> List.map (fun (a,b)-> (a, YamlNodeAdapter(b) :> SchemaNode)) |> Array.ofList
            | _ -> raise <| UnexpectedValueTypeException(value, "map")

        override __.TryGetProperty(prop) =
            match value with
            | Map(items) ->
                items
                |> Seq.tryFind (fst >> ((=) prop))
                |> Option.map (fun (_,x) -> YamlNodeAdapter(x) :> SchemaNode)
            | _ -> None

    let parse = parseYaml >> YamlNodeAdapter

module SwaggerParser =

    let parseJson schema =
        (JsonAdapter.parse schema) :> SchemaNode

    let parseYaml schema =
        (YamlAdapter.parse schema) :> SchemaNode

    let parseSchema (schema:string) : SwaggerObject =
        let parse = 
            if schema.Trim().StartsWith("{")
            then parseJson else parseYaml
        parse schema  |> Parsers.parseSwaggerObject