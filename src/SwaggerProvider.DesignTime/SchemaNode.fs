namespace SwaggerProvider.Internal.Schema.Parsers

[<AbstractClass>]
type SchemaNode() =
    /// Get the boolean value of an element (assuming that value is a boolean)
    abstract member AsBoolean: unit -> bool
    /// Get the string value of an element (assuming that value is a string)
    abstract member AsString: unit -> string
    /// Get all elements of Node element. Returns an empty array if the value is not an array
    abstract member AsArray: unit -> SchemaNode[]
    /// Get the string[] value of an element and exclude 'null' strings (assuming that value is string or string[])
    abstract member AsStringArrayWithoutNull: unit -> string[]

    /// Get the map value of an element (assuming that value is a map)
    abstract member Properties : unit -> (string*SchemaNode)[]
    /// Try get property values from the map by property name
    abstract member TryGetProperty: string -> SchemaNode option

    /// Get field that is `Required` in Swagger specification
    member this.GetRequiredField (fieldName, spec) =
        match this.TryGetProperty(fieldName) with
        | Some(value) -> value
        | None -> raise <| FieldNotFoundException(this, fieldName, spec)

    /// Gets the string value of the property if it exists. Empty string otherwise.
    member this.GetStringSafe(propertyName) =
        match this.TryGetProperty(propertyName) with
        | Some(value) -> value.AsString()
        | None -> ""

    /// Gets the string array for the property if it exists. Empty array otherwise.
    member this.GetStringArraySafe(propertyName) =
        match this.TryGetProperty(propertyName) with
        | Some(value) -> value.AsArray() |> Array.map (fun x->x.AsString())
        | None -> [||]


open FSharp.Data
open FSharp.Data.JsonExtensions

/// Schema node for Swagger schemes in Json format
type JsonNodeAdapter(value:JsonValue) =
    inherit SchemaNode()

    override __.AsBoolean() =
        value.AsBoolean()
    override __.AsString() =
        value.AsString()
    override __.AsArray() =
        value.AsArray() |> Array.map (fun x -> JsonNodeAdapter(x) :> SchemaNode)
    override __.AsStringArrayWithoutNull() =
        match value with
        | JsonValue.String s -> [|s|]
        | JsonValue.Array elements ->
            elements
            |> Array.map (fun x -> x.AsString())
            |> Array.filter (fun x -> x <> "null")
        | other -> failwithf "Value: '%A' cannot be converted to StringArray" other

    override __.Properties() =
        value.Properties |> Array.map (fun (a,b) -> a, JsonNodeAdapter(b) :> SchemaNode)

    override __.TryGetProperty(property) =
        value.TryGetProperty(property)
        |> Option.map (fun x-> JsonNodeAdapter(x) :> SchemaNode)


open SwaggerProvider.YamlParser

/// SchemaNode for Swagger schemes in Yaml format
type YamlNodeAdapter(value:Node) =
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