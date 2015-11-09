namespace SwaggerProvider.Internal.Schema.Parsers

open FSharp.Data
open FSharp.Data.JsonExtensions
open System

open SwaggerProvider.Internal.Schema

/// Helper functions for optional swagger values of types string and string array.
[<AutoOpen>]
module Extensions =
    type JsonValue with
        /// Get field that is `Required` in Swagger specification
        member this.GetRequiredField (fieldName, spec) =
            match this.TryGetProperty(fieldName) with
            | Some(value) -> value
            | None -> raise <| FieldNotFoundException(this, fieldName, spec)

        /// Gets the string value of the property if it exists. Empty string otherwise.
        member this.GetStringSafe(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsString()
            | None -> String.Empty

        /// Gets the string array for the property if it exists. Empty array otherwise.
        member this.GetStringArraySafe(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsArray() |> Array.map (fun x->x.AsString())
            | None -> [||]

module JsonParser =

    /// Parses the JsonValue as an InfoObject.
    let parseInfoObject (obj:JsonValue) : InfoObject =
        let spec = "http://swagger.io/specification/#infoObject"
        {
            Title = obj.GetRequiredField("title", spec).AsString()
            Description = obj.GetStringSafe("description")
            Version = obj.GetRequiredField("version", spec).AsString()
        }

    /// Parses the JsonValue as a TagObject.
    let parseTagObject (obj:JsonValue) : TagObject =
        let spec = "http://swagger.io/specification/#tagObject"
        {
            Name = obj.GetRequiredField("name", spec).AsString()
            Description = obj.GetStringSafe("description")
        }

    // TODO: Validate this parser
    /// Parses the JsonValue as a DefinitionPropertyType.
    let rec parseDefinitionPropertyType (obj:JsonValue) : DefinitionPropertyType =
        let spec = "http://swagger.io/specification/#data-types"

        let parseRef (obj:JsonValue) =
            obj.TryGetProperty("$ref")
            |> Option.map (fun ref ->
                Definition (ref.AsString().Replace("#/definitions/",""))
               )
        let parseIntFormatForType obj format ty =
            match format with
            | "integer"-> Int32
            | "int32"  -> Int32
            | "int64"  -> Int64

            | "int8"   -> Int32 // ??
            | "uint32" -> Int64 // ??
            | "utc-millisec" -> Int64 // ??
            | "id64"   -> Int64 // ??
            | _ -> raise <| UnknownFieldValueException(obj, format, "format", spec)
                   //failwithf "Unsupported `%s` format `%s`" ty x

        match obj.TryGetProperty("type") with
        | Some(ty) ->
            match ty.AsString() with
            | "boolean" -> Boolean
            | "integer" ->
                match obj.TryGetProperty("format") with
                | None -> Int32
                | Some(format) ->
                    parseIntFormatForType obj (format.AsString()) "integer"
            | "number" ->
                match obj.TryGetProperty("format") with
                | None -> Float
                | Some(format) ->
                    match format.AsString() with
                    | "float"   -> Float
                    | "float32" -> Float
                    | "double"  -> Double
                    | strFormat ->
                        parseIntFormatForType obj strFormat "number"
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
                match parseRef obj with
                | Some ref -> Array  ref
                | None -> Array (parseDefinitionPropertyType obj?items)
            | "object" ->
                let elementTy = // TODO: We need to improve this parsing
                    obj.TryGetProperty("additionalProperties")
                    |> Option.map parseDefinitionPropertyType
                Dictionary (defaultArg elementTy Object)
            | "file" -> File
            | x -> failwithf "Unsupported DefinitionPropertyType type %s" x
        | None ->
            match parseRef obj with
            | Some(ref) -> ref
            | None -> Object // TODO: Understand what to do in this case during serialization
                      //failwithf "Unknown DefinitionPropertyType definition %A" obj

    /// Parses string as a OperationParameterLocation.
    let parseOperationParameterLocation obj (location:string) : OperationParameterLocation =
        let spec = "http://swagger.io/specification/#parameterObject"
        match location with
        | "query"    -> Query
        | "header"   -> Header
        | "path"     -> Path
        | "formData" -> FormData
        | "body"     -> Body
        | _ -> raise <| UnknownFieldValueException(obj, location, "location", spec)

    /// Parses the JsonValue as an OperationParameter.
    let parseOperationParameter (obj:JsonValue) : OperationParameter =
        let spec = "http://swagger.io/specification/#parameterObject"
        let location =
            obj.GetRequiredField("in", spec).AsString()
            |> (parseOperationParameterLocation obj)
        {
            Name = obj.GetRequiredField("name", spec).AsString()
            In = location
            Description = obj.GetStringSafe("description")
            Required = match obj.TryGetProperty("required") with
                       | Some(x) -> x.AsBoolean() | None -> false
            Type =
                match location with
                | Body -> obj?schema |> parseDefinitionPropertyType
                | _ -> obj |> parseDefinitionPropertyType // TODO: Parse more options
            CollectionFormat =
                match location, obj.TryGetProperty("collectionFormat") with
                | Body,     Some _                             -> failwith "The field collectionFormat is not apllicable for parameters of type body"
                | _,        Some x when x.AsString() = "csv"   -> Csv
                | _,        Some x when x.AsString() = "ssv"   -> Ssv
                | _,        Some x when x.AsString() = "tsv"   -> Tsv
                | _,        Some x when x.AsString() = "pipes" -> Pipes
                | FormData, Some x when x.AsString() = "multi" -> Multi
                | Query,    Some x when x.AsString() = "multi" -> Multi
                | _,        Some x when x.AsString() = "multi" -> failwith "Format multi is only supported by Query and FormData"
                | _,        Some x                             -> failwithf "Format '%s' is not supported" (x.AsString())
                | _,        None                               -> Csv // Default value
        }

    /// Parses the Json value as an OperationResponse.
    let parseOperationResponse (code, obj:JsonValue) : OperationResponse =
            {
            StatusCode =
                if code = "default" then None
                else code |> Int32.Parse |> Some
            Description = obj.GetStringSafe("description")
            Schema =
                obj.TryGetProperty("schema")
                |> Option.map parseDefinitionPropertyType
        }

    /// Parses the JsonValue as an OperationObject.
    let parseOperationObject (path, opType, obj:JsonValue) : OperationObject =
        {
            Path = path
            Type = opType
            Tags = obj.GetStringArraySafe("tags")
            Summary = obj.GetStringSafe("summary")
            Description = obj.GetStringSafe("description")
            OperationId = obj.GetStringSafe("operationId")
            Consumes = obj.GetStringArraySafe("consumes")
            Produces = obj.GetStringArraySafe("produces")
            Responses =
                (obj?responses).Properties
                |> Array.map parseOperationResponse
            Parameters =
                match obj.TryGetProperty("parameters") with
                | Some(parameters) ->
                    parameters.AsArray() |> Array.map parseOperationParameter
                | None -> [||]
        }

    /// Parses DefinitionProperty
    let parseDefinitionProperty (name, obj, required) : DefinitionProperty =
        {
            Name = name;
            Type = parseDefinitionPropertyType obj
            IsRequired = required
            Description = obj.GetStringSafe("description")
        }

    /// Parses the JsonValue as a Definition.
    let parseDefinition (name:string, obj:JsonValue) =
        let requiredProperties =
            match obj.TryGetProperty("required") with
            | None -> Set.empty<_>
            | Some(req) ->
                req.AsArray()
                |> Array.map (fun x-> x.AsString())
                |> Set.ofArray
        {
            Name = name
            Properties =
                match obj.TryGetProperty("properties") with
                | None -> Array.empty<_>
                | Some(properties) ->
                    properties.Properties
                    |> Array.map (fun (name,obj) ->
                        parseDefinitionProperty (name,obj, requiredProperties.Contains name))
        }

    /// Parses the Json value as a SwaggerSchema.
    let parseSwaggerSchema (obj:JsonValue) =
        let parseOperation (obj:JsonValue) path prop opType =
            obj.TryGetProperty prop
            |> Option.map (fun value->
                parseOperationObject(path, opType, value))
        if (obj?swagger.AsString() <> "2.0") then
            failwith "Swagger version must be 2.0"
        {
            Info = parseInfoObject(obj?info)
            Host = obj.GetStringSafe("host")
            BasePath = obj.GetStringSafe("basePath")
            Schemes = obj.GetStringArraySafe("schemes")
            Tags =
                match obj.TryGetProperty("tags") with
                | None -> [||]
                | Some(tags) ->
                    tags.AsArray() |> Array.map parseTagObject
            Operations =
                match obj.TryGetProperty("paths") with
                | None -> [||]
                | Some(paths) ->
                    paths.Properties
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
                match obj.TryGetProperty("definitions") with
                | None -> Array.empty<_>
                | Some(definitions) ->
                    definitions.Properties
                    |> Array.map parseDefinition
        }