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

        member this.GetStringArray(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsArray() |> Array.map (fun x->x.AsString())
            | None -> [||]

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
    | Dictionary of valTy:DefinitionPropertyType
    | File
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
            | "object" ->
                Dictionary (DefinitionPropertyType.Parse obj?additionalProperties)
            | "file" -> File
            | x -> failwithf "Unsupported property type %s" x
        | None ->
            match obj.TryGetProperty("$ref") with
            | Some(ref) -> Definition (ref.AsString().Replace("#/definitions/",""))
            | None -> failwithf "Unknown property definition %A" obj

type OperationType =
    | Get
    | Put
    | Post
    | Delete
    | Options
    | Head
    | Patch

type OperationParameterLocation =
    | Query
    | Header
    | Path
    | FormData
    | Body

    static member Parse = function
        | "query" -> Query
        | "header" -> Header
        | "path" -> Header
        | "formData" -> FormData
        | "body" -> Body
        | x -> failwithf "Unknown parameter location '%s'" x

type OperationParameter =
    { Name: string
      In: OperationParameterLocation
      Description: string
      Required: bool
      Type: DefinitionPropertyType}

    static member Parse (obj:JsonValue) =
        let location =
            obj.GetProperty("in").AsString()
            |> OperationParameterLocation.Parse
        {
            Name = obj?name.AsString()
            In = location
            Description = obj.GetString("description")
            Required = match obj.TryGetProperty("required") with
                       | Some(x) -> x.AsBoolean() | None -> false
            Type =
                match location with
                | Body -> obj?schema |> DefinitionPropertyType.Parse
                | _ -> obj |> DefinitionPropertyType.Parse // TODO: Parse more options
        }

type OperationResponse =
    { StatusCode: int option
      Description: string
      Schema: DefinitionPropertyType option}

    static member Parse (code, obj:JsonValue) =
        {
            StatusCode =
                if code = "default" then None
                else code |> Int32.Parse |> Some
            Description = obj.GetString("description")
            Schema =
                obj.TryGetProperty("schema")
                |> Option.map DefinitionPropertyType.Parse
        }

type OperationObject =
    { Path: string
      Type: OperationType
      Tags: string[]
      Summary: string
      Description: string
      OperationId: string
      Consumes: string[]
      Produces: string[]
      Responses: OperationResponse[]
      Parameters: OperationParameter[]}

    static member Parse (path, opType, obj:JsonValue) =
        {
            Path = path
            Type = opType
            Tags = obj.GetStringArray("tags")
            Summary = obj.GetString("summary")
            Description = obj.GetString("description")
            OperationId = obj.GetString("operationId")
            Consumes = obj.GetStringArray("consumes")
            Produces = obj.GetStringArray("produces")
            Responses =
                (obj?responses).Properties
                |> Array.map OperationResponse.Parse
            Parameters =
                match obj.TryGetProperty("parameters") with
                | Some(parameters) ->
                    parameters.AsArray() |> Array.map OperationParameter.Parse
                | None -> [||]
        }

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
      Operations: OperationObject[]
      Definitions: Definition[]}

    static member Parse (obj:JsonValue) =
        let parseOperation (obj:JsonValue) path prop opType =
            obj.TryGetProperty prop
            |> Option.map (fun value->
                OperationObject.Parse(path, opType, value))
        {
            Info = InfoObject.Parse(obj?info)
            Tags =
                obj?tags.AsArray()
                |> Array.map TagObject.Parse
            Operations =
                obj?paths.Properties
                |> Array.map (fun (path, pathObj) ->
                     [|parseOperation pathObj path "get"     Get
                       parseOperation pathObj path "put"     Put
                       parseOperation pathObj path "post"    Post
                       parseOperation pathObj path "delete"  Delete
                       parseOperation pathObj path "options" Options
                       parseOperation pathObj path "patch"   Patch|])
                |> Array.concat
                |> Array.choose (id)
            Definitions =
                obj?definitions.Properties
                |> Array.map Definition.Parse
        }