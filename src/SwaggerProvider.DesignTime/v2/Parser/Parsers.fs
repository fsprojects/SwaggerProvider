namespace SwaggerProvider.Internal.v2.Parser

open System
open SwaggerProvider.Internal.v2.Parser.Schema
open System.Collections.Generic

module Exceptions =

    type SwaggerSchemaParseException(message) =
        inherit Exception(message)

    /// Schema object does not contain the `field` that is Required in Swagger specification.
    type FieldNotFoundException<'T>(obj: 'T, field: string, specLink: string) =
        inherit SwaggerSchemaParseException $"Object MUST contain field `%s{field}` (See %s{specLink} for more details).\nObject:%A{obj}"

    /// The `field` value is not specified in Swagger specification
    type UnknownFieldValueException<'T>(obj: 'T, value: string, field: string, specLink: string) =
        inherit SwaggerSchemaParseException $"Value `%s{value}` is not allowed for field `%s{field}`(See %s{specLink} for more details).\nObject:%A{obj}"

    /// The `value` has unexpected type
    type UnexpectedValueTypeException<'T>(obj: 'T, ty: string) =
        inherit SwaggerSchemaParseException $"Expected `%s{ty}` type, but received `%A{obj}`"

    /// Unsupported Swagger version
    type UnsupportedSwaggerVersionException(version) =
        inherit SwaggerSchemaParseException $"SwaggerProviders does not Swagger Specification %s{version}"

    /// Unknown reference
    type UnknownSwaggerReferenceException(ref: string) =
        inherit SwaggerSchemaParseException $"SwaggerProvider could not resolve `$ref`: %s{ref}"

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
    abstract member Properties: unit -> (string * SchemaNode)[]
    /// Try get property values from the map by property name
    abstract member TryGetProperty: string -> SchemaNode option

    /// Get field that is `Required` in Swagger specification
    member this.GetRequiredField(fieldName, spec) =
        match this.TryGetProperty(fieldName) with
        | Some(value) -> value
        | None -> raise <| Exceptions.FieldNotFoundException(this, fieldName, spec)

    /// Gets the string value of the property if it exists. Empty string otherwise.
    member this.GetStringSafe(propertyName) =
        match this.TryGetProperty(propertyName) with
        | Some(value) -> value.AsString()
        | None -> ""

    /// Gets the string array for the property if it exists. Empty array otherwise.
    member this.GetStringArraySafe(propertyName) =
        match this.TryGetProperty(propertyName) with
        | Some(value) -> value.AsArray() |> Array.map(fun x -> x.AsString())
        | None -> [||]

module Parsers =
    open Exceptions

    let emptyDict = Dictionary<string, Lazy<SchemaObject>>()

    // Type that hold parsing context to resolve `$ref`s
    type ParserContext =
        {
            /// An object to hold type definitions
            Definitions: Dictionary<string, Lazy<SchemaObject>>
            /// An object to hold parameters that can be used across operations
            Parameters: Map<string, ParameterObject>
            /// An object to hold responses that can be used across operations.
            Responses: Map<string, ResponseObject>
            /// A list of parameters that are applicable for all the operations described under this path.
            /// These parameters can be overridden at the operation level, but cannot be removed there.
            /// The list MUST NOT include duplicated parameters. A unique parameter is defined by a combination of
            /// a name and location. The list can use the Reference Object to link to parameters that are defined
            /// at the Swagger Object's parameters. There can be one "body" parameter at most.
            ApplicableParameters: ParameterObject[]
        }

        /// Resolve ParameterObject by `$ref` if such field exists
        member this.ResolveParameterObject(obj: SchemaNode) =
            obj.TryGetProperty("$ref")
            |> Option.map(fun refObj ->
                let ref = refObj.AsString()

                match this.Parameters.TryFind(ref) with
                | Some(param) ->
                    match obj.TryGetProperty("required") with
                    | Some(req) ->
                        { param with
                            Required = req.AsBoolean()
                        }
                    | _ -> param
                | None -> raise <| UnknownSwaggerReferenceException(ref))

        // Resolve ResponseObject by `$ref` if such field exists
        member this.ResolveResponseObject(obj: SchemaNode) =
            obj.TryGetProperty("$ref")
            |> Option.map(fun refObj ->
                let ref = refObj.AsString()

                match this.Responses.TryFind(ref) with
                | Some(response) -> response
                | None ->
                    match this.Definitions.TryGetValue(ref) with
                    | true, def -> // Slightly strange use of `ref` from response to `definitions` rather than to `responses`
                        let schema = def.Value

                        {
                            Description = ""
                            Schema = Some(schema)
                        } // TODO: extract description from definition object
                    | _ -> raise <| UnknownSwaggerReferenceException(ref))

        /// Default empty context
        static member Empty = {
            Definitions = emptyDict
            Parameters = Map.empty<_, _>
            Responses = Map.empty<_, _>
            ApplicableParameters = [||]
        }


    /// Verify if name follows Swagger Schema Extension name pattern
    let isSwaggerSchemaExtensionName(name: string) =
        name.StartsWith("x-")

    // TODO: ...
    /// Parses the SchemaNode as a SchemaObject
    let rec parseSchemaObject (definitions: Dictionary<string, Lazy<SchemaObject>>) (obj: SchemaNode) : SchemaObject =
        let spec = "http://swagger.io/specification/#schemaObject"

        let (|IsEnum|_|)(obj: SchemaNode) =
            // Parse `enum` - http://json-schema.org/latest/json-schema-validation.html#anchor76
            obj.TryGetProperty("enum")
            |> Option.map(fun cases -> cases.AsArray() |> Array.map(fun x -> x.AsString()))

        let (|IsRef|_|)(obj: SchemaNode) =
            obj.TryGetProperty("$ref") // Parse `$refs`
            |> Option.map(fun ref -> ref.AsString())

        let (|IsArray|_|)(obj: SchemaNode) =
            // Parse Arrays - http://json-schema.org/latest/json-schema-validation.html#anchor36
            // TODO: `items` may be an array, `additionalItems` may be filled
            obj.TryGetProperty("type")
            |> Option.bind(fun ty ->
                match ty.AsStringArrayWithoutNull() with
                | [| "array" |] -> obj.TryGetProperty("items")
                | _ -> None)
            |> Option.map(parseSchemaObject definitions)

        let (|IsPrimitive|_|)(obj: SchemaNode) =
            // Parse primitive types
            obj.TryGetProperty("type")
            |> Option.bind(fun ty ->
                let format = obj.GetStringSafe("format")

                match ty.AsStringArrayWithoutNull() with
                | [| "boolean" |] -> Some Boolean
                | [| "integer" |] when format = "int32" -> Some Int32
                | [| "integer" |] -> Some Int64
                | [| "number" |] when format = "float" -> Some Float
                | [| "number" |] when format = "int32" -> Some Int32
                | [| "number" |] when format = "int64" -> Some Int64
                | [| "number" |] -> Some Double
                | [| "string" |] when format = "date" -> Some Date
                | [| "string" |] when format = "date-time" -> Some DateTime
                | [| "string" |] when format = "byte" -> Some <| Array Byte
                | [| "string" |] -> Some String
                | [| "file" |] -> Some File
                | _ -> None)

        let (|IsObject|_|)(obj: SchemaNode) =
            // TODO: Parse Objects
            obj.TryGetProperty("properties")
            |> Option.map(fun properties ->
                let requiredProperties =
                    match obj.TryGetProperty("required") with
                    | None -> Set.empty<_>
                    | Some(req) -> req.AsArray() |> Array.map(fun x -> x.AsString()) |> Set.ofArray

                let properties =
                    properties.Properties()
                    |> Array.map(fun (name, obj) -> parseDefinitionProperty definitions (name, obj, requiredProperties.Contains name))

                properties)

        let (|IsDict|_|)(obj: SchemaNode) =
            // Parse Object that represent Dictionary
            match obj.TryGetProperty("type") with
            | Some(ty) when ty.AsStringArrayWithoutNull() = [| "object" |] ->
                obj.TryGetProperty("additionalProperties")
                |> Option.bind(fun obj ->
                    match parseSchemaObject definitions obj with
                    | Object [||] -> None
                    | schemaObj -> Some schemaObj)
            | _ -> None

        let (|IsAllOf|_|)(obj: SchemaNode) =
            // Identify composition element 'allOf'
            obj.TryGetProperty("allOf") |> Option.map(fun x -> x.AsArray())

        let (|IsComposition|_|)(obj: SchemaNode) =
            // Models with Object Composition
            match obj with
            | IsAllOf allOf ->
                let components =
                    allOf
                    |> Array.map(fun x ->
                        match parseSchemaObject definitions x with
                        | Object props -> Some props
                        | Reference path ->
                            match definitions.TryGetValue path with
                            | true, lazeObj ->
                                match lazeObj.Value with
                                | Object props -> Some props
                                | _ -> None
                            | _ -> failwithf $"Reference to unknown type %s{path}"
                        | _ -> None)

                if components |> Array.forall(Option.isSome) then
                    components |> Array.choose id |> Array.concat |> Some
                else
                    None // One of elements is not an Object and we cannot Compose

            | _ -> None

        let (|IsWrapper|_|)(obj: SchemaNode) =
            // Support for obj that wrap another obj / primitive
            // Sample https://github.com/APIs-guru/openapi-directory/issues/98
            match obj with
            | IsAllOf allOf when allOf.Length = 1 ->
                parseSchemaObject definitions allOf.[0]
                |> function
                    | Reference path ->
                        match definitions.TryGetValue path with
                        | true, lazeObj -> Some <| lazeObj.Value
                        | _ -> failwithf $"Reference to unknown type %s{path}"
                    | _ -> None
            | _ -> None

        let (|IsPolymorphism|_|)(obj: SchemaNode) =
            // Models with Polymorphism Support
            obj.TryGetProperty("discriminator")

        match obj with
        | IsEnum cases ->
            let ty =
                obj.TryGetProperty("type")
                |> Option.map(fun x -> x.AsString())
                |> Option.defaultValue "string"

            Enum(cases, ty)
        | IsRef ref -> Reference ref
        | IsArray itemTy -> Array itemTy
        | IsPrimitive ty -> ty
        | IsDict itemTy -> SchemaObject.Dictionary itemTy
        | IsObject objProps & IsComposition compProps -> Object <| Array.append compProps objProps
        | IsObject props -> Object props
        | IsComposition props -> Object props
        | IsWrapper ty -> ty
        | IsPolymorphism _ ->
            failwith
                "Models with Polymorphism Support is not supported yet. If you see this error please report it on GitHub (https://github.com/fsprojects/SwaggerProvider/issues) with schema example."
        | _ -> Object [||] // Default type when parsers could not determine the type based ob schema.
    // Example of schema : {}


    /// Parses DefinitionProperty
    and parseDefinitionProperty parsedTys (name, obj, required) : DefinitionProperty = {
        Name = name
        Type = parseSchemaObject parsedTys obj
        IsRequired = required
        Description = obj.GetStringSafe("description")
    }

    /// Parses string as a ParameterObjectLocation.
    let parseOperationParameterLocation obj (location: string) : ParameterObjectLocation =
        let spec = "http://swagger.io/specification/#parameterObject"

        match location with
        | "query" -> Query
        | "header" -> Header
        | "path" -> Path
        | "formData" -> FormData
        | "body" -> Body
        | _ -> raise <| UnknownFieldValueException(obj, location, "in", spec)

    /// Parses the SchemaNode as a ParameterObject.
    let parseParameterObject (definitions: Dictionary<string, Lazy<SchemaObject>>) (obj: SchemaNode) : ParameterObject =
        let spec = "http://swagger.io/specification/#parameterObject"

        let location =
            obj.GetRequiredField("in", spec).AsString()
            |> (parseOperationParameterLocation obj)

        {
            Name = obj.GetRequiredField("name", spec).AsString()
            In = location
            Description = obj.GetStringSafe("description")
            Required =
                match obj.TryGetProperty("required") with
                | Some(x) -> x.AsBoolean()
                | None -> false
            Type =
                match location with
                | Body -> obj.GetRequiredField("schema", spec) |> parseSchemaObject definitions
                | _ -> obj |> parseSchemaObject definitions
            // The `type` value MUST be one of "string", "number", "integer", "boolean", "array" or "file"
            CollectionFormat =
                match location, obj.TryGetProperty("collectionFormat") with
                | Body, Some _ -> failwith "The field collectionFormat is not applicable for parameters of type body"
                | _, Some x when x.AsString() = "csv" -> Csv
                | _, Some x when x.AsString() = "ssv" -> Ssv
                | _, Some x when x.AsString() = "tsv" -> Tsv
                | _, Some x when x.AsString() = "pipes" -> Pipes
                | FormData, Some x when x.AsString() = "multi" -> Multi
                | Query, Some x when x.AsString() = "multi" -> Multi
                | _, Some x when x.AsString() = "multi" -> failwith "Format `multi` is only supported by Query and FormData"
                | _, Some x -> failwithf $"Format `%s{x.AsString()}` is not supported"
                | _, None -> Csv // Default value
        }

    /// Parse the SchemaNode as a Parameters Definition Object
    let parseParametersDefinition (definitions: Dictionary<string, Lazy<SchemaObject>>) (obj: SchemaNode) : Map<string, ParameterObject> =
        obj.Properties()
        |> Array.map(fun (name, obj) -> "#/parameters/" + name, parseParameterObject definitions obj)
        |> Map.ofArray

    /// Parses the SchemaNode as a ResponseObject.
    let parseResponseObject (context: ParserContext) (obj: SchemaNode) : ResponseObject =
        let spec = "http://swagger.io/specification/#responseObject"

        match context.ResolveResponseObject obj with
        | Some(response) -> response
        | None -> {
            Description = obj.GetRequiredField("description", spec).AsString()
            Schema =
                obj.TryGetProperty("schema")
                |> Option.map(parseSchemaObject context.Definitions)
          }

    /// Parses the SchemaNode as a Responses  Definition Object
    let parseResponsesDefinition(obj: SchemaNode) : Map<string, ResponseObject> =
        obj.Properties()
        |> Array.map(fun (name, obj) -> "#/responses/" + name, parseResponseObject (ParserContext.Empty) obj)
        |> Map.ofSeq

    /// Parses the SchemaNode as a ResponseObject[].
    let parseResponsesObject (context: ParserContext) (obj: SchemaNode) : (Option<int> * ResponseObject)[] =
        let spec = "http://swagger.io/specification/#httpCodes"

        obj.Properties()
        |> Array.filter(fun (property, _) -> not <| isSwaggerSchemaExtensionName property)
        |> Array.map(fun (property, objValue) ->
            let code =
                if property = "default" then
                    None
                else
                    match Int32.TryParse(property) with
                    | true, value -> Some value
                    | false, _ ->
                        raise
                        <| UnknownFieldValueException(obj, property, "HTTP Status Code", spec)

            code, parseResponseObject context objValue)

    /// Parses the SchemaNode as an OperationObject.
    let parseOperationObject (context: ParserContext) path opType (obj: SchemaNode) : OperationObject =
        let spec = "http://swagger.io/specification/#operationObject"

        let mergeParameters (specified: ParameterObject[]) (inherited: ParameterObject[]) =
            Array.append specified inherited
            |> Array.fold
                (fun (cache, result) param ->
                    let key = (param.Name, param.In)

                    if Set.contains key cache then
                        (cache, result)
                    else
                        (Set.add key cache, param :: result))
                (Set.empty<_>, [])
            |> snd
            |> List.rev
            |> Array.ofList

        {
            Path = path
            Type = opType
            Tags = obj.GetStringArraySafe("tags")
            Summary = obj.GetStringSafe("summary")
            Description = obj.GetStringSafe("description")
            OperationId = obj.GetStringSafe("operationId")
            Consumes = obj.GetStringArraySafe("consumes")
            Produces = obj.GetStringArraySafe("produces")
            Deprecated =
                match obj.TryGetProperty("deprecated") with
                | Some(value) -> value.AsBoolean()
                | None -> false
            Responses =
                obj.GetRequiredField("responses", spec)
                |> (parseResponsesObject context)
            Parameters =
                mergeParameters
                    (match obj.TryGetProperty("parameters") with
                     | Some(parameters) ->
                         parameters.AsArray()
                         |> Array.map(fun obj ->
                             match context.ResolveParameterObject obj with
                             | Some(param) -> param
                             | None -> parseParameterObject context.Definitions obj)
                     | None -> [||])
                    context.ApplicableParameters
        }

    /// Parse the SchemaNode as a PathItemObject[]
    let parsePathsObject (context: ParserContext) (obj: SchemaNode) : OperationObject[] =
        let parsePathItemObject (context: ParserContext) path (field, obj) =
            match field with
            | "get" -> Some <| parseOperationObject context path Get obj
            | "put" -> Some <| parseOperationObject context path Put obj
            | "post" -> Some <| parseOperationObject context path Post obj
            | "delete" -> Some <| parseOperationObject context path Delete obj
            | "options" -> Some <| parseOperationObject context path Options obj
            | "head" -> Some <| parseOperationObject context path Head obj
            | "patch" -> Some <| parseOperationObject context path Patch obj
            | "$ref" -> failwith "External definition of this path item is not supported yet"
            | _ -> None

        let updateContext(pathItemObj: SchemaNode) =
            match pathItemObj.TryGetProperty("parameters") with
            | None -> context
            | Some(parameters) ->
                { context with
                    ApplicableParameters =
                        parameters.AsArray()
                        |> Array.map(fun paramObj ->
                            match context.ResolveParameterObject paramObj with
                            | Some(param) -> param
                            | None -> parseParameterObject context.Definitions paramObj)
                }

        obj.Properties()
        |> Array.filter(fun (path, _) -> not <| isSwaggerSchemaExtensionName path)
        |> Array.collect(fun (path, pathItemObj) ->
            let newContext = updateContext pathItemObj

            pathItemObj.Properties()
            |> Array.choose(parsePathItemObject newContext path))

    /// Parse the SchemaNode as a SchemaObject[]
    let parseDefinitionsObject(obj: SchemaNode) : Dictionary<string, Lazy<SchemaObject>> =
        let defs = Dictionary<string, Lazy<SchemaObject>>()

        obj.Properties()
        |> Array.iter(fun (name, schemaObj) -> defs.Add("#/definitions/" + name, lazy (parseSchemaObject defs schemaObj)))

        defs

    /// Parses the SchemaNode as an InfoObject.
    let parseInfoObject(obj: SchemaNode) : InfoObject =
        let spec = "http://swagger.io/specification/#infoObject"

        {
            Title = obj.GetRequiredField("title", spec).AsString()
            Description = obj.GetStringSafe("description")
            Version = obj.GetRequiredField("version", spec).AsString()
        }

    /// Parses the SchemaNode as a TagObject.
    let parseTagObject(obj: SchemaNode) : TagObject =
        let spec = "http://swagger.io/specification/#tagObject"

        {
            Name = obj.GetRequiredField("name", spec).AsString()
            Description = obj.GetStringSafe("description")
        }

    /// Parses the SchemaNode as a SwaggerSchema.
    let parseSwaggerObject(obj: SchemaNode) : SwaggerObject =
        let spec = "http://swagger.io/specification/#swaggerObject"

        let swaggerVersion = obj.GetRequiredField("swagger", spec).AsString()

        if swaggerVersion <> "2.0" then
            raise <| UnsupportedSwaggerVersionException(swaggerVersion)

        // Context holds parameters and responses that could be referenced from path definitions
        let context =
            let definitions =
                match obj.TryGetProperty("definitions") with
                | None -> emptyDict
                | Some(definitions) -> parseDefinitionsObject definitions

            { ParserContext.Empty with
                Definitions = definitions
                Parameters =
                    match obj.TryGetProperty("parameters") with
                    | None -> Map.empty<_, _>
                    | Some(parameters) -> parseParametersDefinition definitions parameters
                Responses =
                    match obj.TryGetProperty("responses") with
                    | None -> Map.empty<_, _>
                    | Some(responses) -> parseResponsesDefinition responses
            }

        {
            Info = parseInfoObject(obj.GetRequiredField("info", spec))
            Host = obj.GetStringSafe("host")
            BasePath = obj.GetStringSafe("basePath")
            Schemes = obj.GetStringArraySafe("schemes")
            Tags =
                match obj.TryGetProperty("tags") with
                | None -> [||]
                | Some(tags) -> tags.AsArray() |> Array.map parseTagObject
            Paths = obj.GetRequiredField("paths", spec) |> (parsePathsObject context)
            Definitions =
                context.Definitions
                |> Seq.map(fun x -> x.Key, x.Value.Value)
                |> Seq.sortBy(id)
                |> Array.ofSeq
        }
