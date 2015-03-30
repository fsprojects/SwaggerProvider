namespace SwaggerProvider.Internal.Schema

open FSharp.Data
open FSharp.Data.Runtime.NameUtils
open FSharp.Data.JsonExtensions
open System

[<AutoOpen>]
module Extensions =
    type JsonValue with
        member this.GetString(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsString()
            | None -> String.Empty

type InfoObject =
    { Title: string
      Description: string
      Version: string}

    static member Parse (obj:JsonValue) =
        {
            Title = obj?title.AsString()
            Description = obj.GetString("description")
            Version = obj?version.AsString()
        }

type TagObject =
    { Name: string
      Description: string}

    static member Parse (obj:JsonValue) =
        {
            Name = obj?name.AsString()
            Description = obj.GetString("description")
        }

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
            | Some(ref) -> Definition (ref.AsString().Replace("#/definitions/",""))
            | None -> failwithf "Unknown property definition %A" obj


type DefinitionProperty =
    { Name: string
      Type: DefinitionPropertyType
      IsRequired : bool
      Description: string}

    static member Parse (name, obj, required) =
        {
            Name = name;
            Type = DefinitionPropertyType.Parse obj
            IsRequired = required
            Description = obj.GetString("description")
        }


type Definition =
    { Name: string
      Properties: DefinitionProperty[] }

    static member Parse (name:string, obj:JsonValue) =
        let requiredProperties =
            match obj.TryGetProperty("required") with
            | Some(req) ->
                req.AsArray()
                |> Array.map (fun x-> x.AsString())
                |> Set.ofArray
            | None -> Set.empty<_>
        {
            Name = name
            Properties =
                obj?properties.Properties
                |> Array.map (fun (name,obj) ->
                    DefinitionProperty.Parse (name,obj, requiredProperties.Contains name))
        }


type SwaggerSchema =
    { Info: InfoObject
      Tags: TagObject[]
      Definitions: Definition[]}

    static member Parse (obj:JsonValue) =
        {
            Info = InfoObject.Parse(obj?info)
            Tags =
                obj?tags.AsArray()
                |> Array.map TagObject.Parse
            Definitions =
                obj?definitions.Properties
                |> Array.map Definition.Parse
        }