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

    // Type that hold parsing context to resolve `$ref`s
    type ParserContext =
        {
            /// An object to hold parameters that can be used across operations
            Parameters: Map<string, ParameterObject>
            /// An object to hold responses that can be used across operations.
            Responses: Map<string, OperationResponse>
            /// A list of parameters that are applicable for all the operations described under this path.
            /// These parameters can be overridden at the operation level, but cannot be removed there.
            /// The list MUST NOT include duplicated parameters. A unique parameter is defined by a combination of
            /// a name and location. The list can use the Reference Object to link to parameters that are defined
            /// at the Swagger Object's parameters. There can be one "body" parameter at most.
            ApplicableParameters : ParameterObject[]
        }

        /// Resolve Parameter by `$ref` in such field exists
        member this.ResolveParameter (obj:JsonValue) =
            obj.TryGetProperty("$ref")
            |> Option.map (fun refObj ->
                let ref = refObj.AsString()
                match this.Parameters.TryFind(ref) with
                | Some(param) ->
                    match obj.TryGetProperty("required") with
                    | Some(req) -> {param with Required = req.AsBoolean()}
                    | _ -> param
                | None -> raise <| UnknownSwaggerReferenceException(ref))

        /// Default empty context
        static member Empty =
            {
                Parameters = Map.empty<_,_>
                Responses = Map.empty<_,_>
                ApplicableParameters = [||]
            }


    /// Verify if name follows Swagger Schema Extension name pattern
    let isSwaggerSchemaExtensionName (name:string) =
        name.StartsWith("x-")

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
                | None ->
                    match obj.TryGetProperty("items") with
                    | Some(items) -> Array (parseDefinitionPropertyType items)
                    | None ->
                        match obj.TryGetProperty("enum") with
                        | Some(enum) ->
                            Enum (enum.AsArray() |> Array.map (fun x->x.AsString()))
                        | None -> failwithf "Could not parse type of array elements\nArray:%A" obj
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

    /// Parses string as a ParameterObjectLocation.
    let parseOperationParameterLocation obj (location:string) : ParameterObjectLocation =
        let spec = "http://swagger.io/specification/#parameterObject"
        match location with
        | "query"    -> Query
        | "header"   -> Header
        | "path"     -> Path
        | "formData" -> FormData
        | "body"     -> Body
        | _ -> raise <| UnknownFieldValueException(obj, location, "in", spec)

    // TODO:
    /// Parses the JsonValue as an ParameterObject.
    let parseParameterObject (obj:JsonValue) : ParameterObject =
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

    // TODO:
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
    let parseOperationObject (context:ParserContext) path opType (obj:JsonValue) : OperationObject =
        let spec = "http://swagger.io/specification/#operationObject"
        let mergeParameters (specified:ParameterObject[]) (inherited:ParameterObject[]) =
            Array.append specified inherited
            |> Array.fold (fun (cache,result) param ->
                let key = (param.Name, param.In)
                if Set.contains key cache
                    then (cache, result)
                    else (Set.add key cache, param::result)
                ) (Set.empty<_>,[])
            |> snd |> List.rev |> Array.ofList

        {
            Path = path
            Type = opType
            Tags = obj.GetStringArraySafe("tags")
            Summary = obj.GetStringSafe("summary")
            Description = obj.GetStringSafe("description")
            OperationId = obj.GetStringSafe("operationId")
            Consumes = obj.GetStringArraySafe("consumes")
            Produces = obj.GetStringArraySafe("produces")
            Deprecated = match obj.TryGetProperty("deprecated") with
                         | Some(value) -> value.AsBoolean()
                         | None -> false
            Responses =
                Array.append
                    (obj.GetRequiredField("responses", spec).Properties
                     |> Array.map parseOperationResponse)
                    (context.Responses |> Map.toArray |> Array.map snd)
            Parameters =
                mergeParameters
                   (match obj.TryGetProperty("parameters") with
                        | Some(parameters) ->
                            parameters.AsArray()
                            |> Array.map (fun obj ->
                                match context.ResolveParameter obj with
                                | Some(param) -> param
                                | None -> parseParameterObject obj)
                        | None -> [||])
                   context.ApplicableParameters
        }

    // TODO:
    /// Parses DefinitionProperty
    let parseDefinitionProperty (name, obj, required) : DefinitionProperty =
        {
            Name = name;
            Type = parseDefinitionPropertyType obj
            IsRequired = required
            Description = obj.GetStringSafe("description")
        }

    // TODO:
    /// Parses the JsonValue as a SchemaObject.
    let parseSchemaObject (name:string, obj:JsonValue) : SchemaObject =
        let spec = "http://swagger.io/specification/#schemaObject"
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

    /// Parse Paths Object as PathItemObject[]
    let parsePathsObject (context:ParserContext) (obj:JsonValue) : OperationObject[] =
        let parsePathItemObject (context:ParserContext) path (field, obj) =
            match field with
            | "get"     -> Some <| parseOperationObject context path Get obj
            | "put"     -> Some <| parseOperationObject context path Put obj
            | "post"    -> Some <| parseOperationObject context path Post obj
            | "delete"  -> Some <| parseOperationObject context path Delete obj
            | "options" -> Some <| parseOperationObject context path Options obj
            | "head"    -> Some <| parseOperationObject context path Head obj
            | "patch"   -> Some <| parseOperationObject context path Patch obj
            | "$ref"       -> failwith "External definition of this path item is not supported yet"
            | _ -> None
        let updateContext (pathItemObj:JsonValue) =
            match pathItemObj.TryGetProperty("parameters") with
            | None -> context
            | Some(parameters) ->
                {
                context with
                    ApplicableParameters =
                        parameters.AsArray()
                        |> Array.map (fun paramObj ->
                            match context.ResolveParameter paramObj with
                            | Some(param) -> param
                            | None -> parseParameterObject paramObj
                        )
                }

        obj.Properties
        |> Array.filter(fun (path,_) -> not <| isSwaggerSchemaExtensionName path)
        |> Array.map (fun (path, pathItemObj) ->
            let newContext = updateContext pathItemObj
            pathItemObj.Properties
            |> Array.choose (parsePathItemObject newContext path)
           )
        |> Array.concat

    /// Parse Definitions Object as SchemaObject[]
    let parseDefinitionsObject (obj:JsonValue) : SchemaObject[] =
        obj.Properties
        |> Array.map parseSchemaObject

    /// Parses the JsonValue as a SwaggerSchema.
    let parseSwaggerObject (obj:JsonValue) : SwaggerObject =
        let spec = "http://swagger.io/specification/#swaggerObject"

        let swaggerVersion = obj.GetRequiredField("swagger", spec).AsString()
        if swaggerVersion <> "2.0" then
            raise <| UnsupportedSwaggerVersionException(swaggerVersion)

        // Context holds parameters and responses that could be referenced from path definitions
        let context =
            {
                ParserContext.Empty with
                    Parameters =
                        match obj.TryGetProperty("parameters") with
                        | None -> Map.empty<_,_>
                        | Some(parameters) ->
                            parameters.Properties
                            |> Array.map (fun (name, obj) ->
                                "#/parameters/"+name, parseParameterObject obj)
                            |> Map.ofArray
                    Responses =
                        match obj.TryGetProperty("responses") with
                        | None -> Map.empty<_,_>
                        | Some(responses) ->
                            responses.Properties
                            |> Array.map (fun (name, obj) ->
                                 "#/responses/"+name, parseOperationResponse (name, obj))
                            |> Map.ofSeq
            }

        {
            Info = parseInfoObject(obj.GetRequiredField("info", spec))
            Host = obj.GetStringSafe("host")
            BasePath = obj.GetStringSafe("basePath")
            Schemes = obj.GetStringArraySafe("schemes")
            Tags =
                match obj.TryGetProperty("tags") with
                | None -> [||]
                | Some(tags) ->
                    tags.AsArray() |> Array.map parseTagObject
            Paths =
                obj.GetRequiredField("paths", spec)
                |> (parsePathsObject context)
            Definitions =
                match obj.TryGetProperty("definitions") with
                | None -> [||]
                | Some(definitions) -> parseDefinitionsObject definitions
        }