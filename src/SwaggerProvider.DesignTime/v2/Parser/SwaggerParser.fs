namespace SwaggerProvider.Internal.v2.Parser

open SwaggerProvider.Internal.v2.Parser.Schema
open SwaggerProvider.Internal.v2.Parser.Exceptions

module internal JsonAdapter =
    open System.Text.Json

    /// Schema node for Swagger schemes in Json format
    type JsonNodeAdapter(value:JsonElement) =
        inherit SchemaNode()

        override __.AsBoolean() = value.GetBoolean()

        override __.AsString() = value.GetString()

        override __.AsArray() =
            match value.ValueKind with
            | JsonValueKind.Array ->
                [|for item in value.EnumerateArray() do JsonNodeAdapter(item) :> SchemaNode|]
            | _ -> raise <| UnexpectedValueTypeException(value, "string")

        override __.AsStringArrayWithoutNull() =
            match value.ValueKind with
            | JsonValueKind.String ->
                [|value.GetString()|]
            | JsonValueKind.Array ->
                [|for item in value.EnumerateArray() do item.GetString()|]
                |> Seq.filter (fun x -> x <> "null")
                |> Seq.toArray
            | other ->
                failwithf "Value: '%A' cannot be converted to StringArray" other

        override __.Properties() =
            match value.ValueKind with
            | JsonValueKind.Object ->
                [|for item in value.EnumerateObject() do item.Name, JsonNodeAdapter(item.Value) :> SchemaNode|]
            | _ -> raise <| UnexpectedValueTypeException(value, "Object")

        override __.TryGetProperty(property) =
            match value.ValueKind with
            | JsonValueKind.Object ->
                match value.TryGetProperty(property) with
                | true, x -> Some(JsonNodeAdapter(x) :> SchemaNode)
                | _ -> None
            | _ -> None

    let parse (string: string) = (JsonDocument.Parse string).RootElement |> JsonNodeAdapter

module internal YamlAdapter =
    open System.IO
    open YamlDotNet.Serialization
    open System.Collections.Generic

    let (|List|_|) (node: obj) =
        match node with
        | :? List<obj> as l -> Some l
        | _ -> None

    let (|Map|_|) (node: obj) =
        match node with
        | :? Dictionary<obj,obj> as dict ->
            dict
            |> Seq.choose (fun p ->
                match p.Key with
                | :? string as key -> Some (key, p.Value)
                | _ -> None)
            |> Some
        | _ -> None

    let (|Scalar|_|) (node: obj) =
        match node with
        | :? List<obj>
        | :? Dictionary<obj,obj> ->
            None
        | scalar ->
            let value = if isNull scalar then "" else scalar.ToString()
            Some (value)

    /// SchemaNode for Swagger schemes in Yaml format
    type YamlNodeAdapter(value:obj) =
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
                nodes |> Seq.map(fun x->YamlNodeAdapter(x) :> SchemaNode) |> Array.ofSeq
            | _ -> [||]

        override __.AsStringArrayWithoutNull() =
            match value with
            | Scalar(x) -> [|x|]
            | List(nodes) ->
                nodes
                |> Seq.map(function
                    | Scalar (x) -> x
                    | x -> failwithf "'%A' cannot be converted to string" x)
                |> Seq.filter (fun x -> x <> "null")
                |> Seq.toArray
            | other -> failwithf "Value: '%A' cannot be converted to StringArray" other

        override __.Properties() =
            match value with
            | Map(pairs) -> pairs |> Seq.map (fun (a,b)-> (a, YamlNodeAdapter(b) :> SchemaNode)) |> Array.ofSeq
            | _ -> raise <| UnexpectedValueTypeException(value, "map")

        override __.TryGetProperty(prop) =
            match value with
            | Map(items) ->
                items
                |> Seq.tryFind (fst >> ((=) prop))
                |> Option.map (fun (_,x) -> YamlNodeAdapter(x) :> SchemaNode)
            | _ -> None

    let private deserializer = Deserializer()
    let parse (text:string) =
        try
            use reader = new StringReader(text)
            deserializer.Deserialize(reader) |> YamlNodeAdapter
        with
        | :? YamlDotNet.Core.YamlException as e when not <| isNull e.InnerException ->
            raise e.InnerException // inner exceptions are much more informative
        | _ -> reraise()

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
