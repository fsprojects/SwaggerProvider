namespace SwaggerProvider.Schema

open FSharp.Data
open FSharp.Data.Runtime.NameUtils
open FSharp.Data.JsonExtensions

//https://github.com/swagger-api/swagger-spec/blob/master/versions/2.0.md#data-types
type DefinitionPropertyType =
    | Boolean
    | Int32
    | Int64
    | Float
    | Double
    | String
    | Date
    | DateTime
    | Enum of values:string[]
    | Array of itemTy:DefinitionPropertyType
    | Definition of name:string

    static member Parse (obj:JsonValue) =
        match obj.TryGetProperty("type") with
        | Some(ty) ->
            match ty.AsString() with
            | "boolean" -> Boolean
            | "integer" ->
                match obj?format.AsString() with
                | "int32" -> Int32
                | "int64" -> Int64
                | x -> failwithf "Unsupported `integer` format %s" x
            | "number" ->
                match obj?format.AsString() with
                | "float" -> Float
                | "double" -> Double
                | x -> failwithf "Unsupported `number` format %s" x
            | "string" ->
                match obj.TryGetProperty("format") with
                | None ->
                    match obj.TryGetProperty("enum") with
                    | Some(enum) ->
                        Enum (enum.AsArray() |> Array.map (fun x->x.AsString()))
                    | None -> String
                | Some(format) ->
                    match format.AsString() with
                    | "date" -> Date
                    | "date-time" -> DateTime
                    | _ -> String
            | "array" ->
                Array (DefinitionPropertyType.Parse obj?items)
            | x -> failwith "Unsupported property type %s" x
        | None ->
            match obj.TryGetProperty("$ref") with
            | Some(ref) -> Definition (ref.AsString())
            | None -> failwith "Unknown property definition %A" obj


type DefinitionProperty =
    { Name: string
      Type: DefinitionPropertyType
      Description: string}

    static member Parse (name:string, obj:JsonValue) =
        {
        Name = name;
        Type = DefinitionPropertyType.Parse obj
        Description = obj?description.AsString();
        }


type Definition =
    { Name: string
      Properties: DefinitionProperty[] }

    static member Parse (name:string, obj:JsonValue) =
        {
        Name = name
        Properties =
            obj.Properties
            |> Array.map DefinitionProperty.Parse
        }