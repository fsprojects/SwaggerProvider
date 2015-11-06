/// Internal schema used to parse json objects to internal swagger objects.
namespace SwaggerProvider.Internal.Schema

open FSharp.Data
open FSharp.Data.Runtime.NameUtils
open FSharp.Data.JsonExtensions
open System

/// Helper functions for optional swagger values of types string and string array.
[<AutoOpen>]
module Extensions =
    type JsonValue with
        /// Gets the string value of the property if it exists. Empty string otherwise.
        member this.GetString(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsString()
            | None -> String.Empty

        /// Gets the string array for the property if it exists. Empty array otherwise.
        member this.GetStringArray(propertyName) =
            match this.TryGetProperty(propertyName) with
            | Some(value) -> value.AsArray() |> Array.map (fun x->x.AsString())
            | None -> [||]

/// Basic swagger information, relevant to the type provider.
/// http://swagger.io/specification/#infoObject
type InfoObject =
    { /// Required. The title of the application.
      Title: string
      /// A short description of the application.
      Description: string
      /// Required Provides the version of the application API (not to be confused with the specification version).
      Version: string}

    /// Parses the Json value as an InfoObject.
    static member Parse (obj:JsonValue) =
        {
            Title = obj?title.AsString()
            Description = obj.GetString("description")
            Version = obj?version.AsString()
        }

/// Allows adding meta data to a single tag.
/// http://swagger.io/specification/#tagObject
type TagObject =
    { /// Required. The name of the tag.
      Name: string
      /// A short description for the tag.
      Description: string}

    /// Parses the Json value as a TagObject.
    static member Parse (obj:JsonValue) =
        {
            Name = obj?name.AsString()
            Description = obj.GetString("description")
        }

//https://github.com/swagger-api/swagger-spec/blob/master/versions/2.0.md#data-types
/// Primitive data types from the Swagger Specification.
/// http://swagger.io/specification/#data-types
type DefinitionPropertyType =
    /// Boolean.
    | Boolean
    /// Integer (signed 32 bits).
    | Int32
    /// Long (signed 64 bits).
    | Int64
    /// Float.
    | Float
    /// Double.
    | Double
    /// String.
    | String
    /// Date (As defined by full-date - RFC3339).
    | Date
    /// Date-Time (As defined by date-time - RFC3339).
    | DateTime
    /// Enumeration
    | Enum of values:string[]
    /// Array of items of type itemTy
    | Array of itemTy:DefinitionPropertyType
    /// Dictionary / Map
    | Dictionary of valTy:DefinitionPropertyType
    /// An additional primitive data type used by the Parameter Object and the Response Object to set the parameter type or the response as being a file.
    | File
    /// A defintion of an object defined by a Schema Object.
    | Definition of name:string

    /// Parses the Json value as a DefinitionPropertyType.
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

    override this.ToString() =
        match this with
        | Boolean      -> "Boolean"
        | Int32        -> "Int32"
        | Int64        -> "Int64"
        | Float        -> "Float"
        | Double       -> "Double"
        | String       -> "String"
        | Date         -> "Date"
        | DateTime     -> "DateTime"
        | Enum _       -> "Enum"
        | Array x      -> "Array " + x.ToString()
        | Dictionary x -> "Dictionary " + x.ToString()
        | File         -> "File"
        | Definition s -> "Definition " + s

/// The type of the REST call.
/// http://swagger.io/specification/#pathItemObject
type OperationType =

    /// Returns en element or collection.
    | Get
    /// Updates an element.
    | Put
    /// Adds an element.
    | Post
    /// Removes an element.
    | Delete
    | Options
    | Head
    | Patch

    override this.ToString() =
        match this with
        | Get     -> "Get"
        | Put     -> "Put"
        | Post    -> "Post"
        | Delete  -> "Delete"
        | Options -> "Options"
        | Head    -> "Head"
        | Patch   -> "Patch"

/// Determines the format of the array if type array is used. Array value separator.
type CollectionFormat =
    /// Comma separated values.
    | Csv
    /// Space separated values.
    | Ssv
    /// Tab separated values.
    | Tsv
    /// Pipe separated values.
    | Pipes
    /// Corresponds to multiple parameter instances instead of multiple values for a single instance.
    | Multi

    override this.ToString() =
        match this with
        | Csv   -> ","
        | Ssv   -> " "
        | Tsv   -> "\t"
        | Pipes -> "|"
        | Multi -> failwith "CollectionFormat 'Multi' does not support ToString()"

/// Required. The location of the parameter.
type OperationParameterLocation =
    /// Parameter that are appended to the URL. For example, in /items?id=###, the query parameter is id.
    | Query
    /// Custom header that are expected as part of the request.
    | Header
    /// Used together with Path Templating, where the parameter value is actually part of the operation's URL. This does not include the host or base path of the API. For example, in /items/{itemId}, the path parameter is itemId.
    | Path
    /// Used to describe the payload of an HTTP request.
    | FormData
    /// The payload that's appended to the HTTP request. Since there can only be one payload, there can only be one body parameter. The name of the body parameter has no effect on the parameter itself and is used for documentation purposes only. Since Form parameters are also in the payload, body and form parameters cannot exist together for the same operation.
    | Body

    static member Parse = function
        | "query"    -> Query
        | "header"   -> Header
        | "path"     -> Path
        | "formData" -> FormData
        | "body"     -> Body
        | x          -> failwithf "Unknown parameter location '%s'" x

/// Describes a single operation parameter.
/// http://swagger.io/specification/#parameterObject
type OperationParameter =
    { /// Required. The name of the parameter. Parameter names are case sensitive.
      /// If in is "path", the name field MUST correspond to the associated path segment from the path field in the Paths Object. See Path Templating for further information.
      /// For all other cases, the name corresponds to the parameter name used based on the in property.
      Name: string
      /// Required. The location of the parameter.
      In: OperationParameterLocation
      /// A brief description of the parameter. This could contain examples of use.
      Description: string
      /// Determines whether this parameter is mandatory. If the parameter is in "path", this property is required and its value MUST be true. Otherwise, the property MAY be included and its default value is false.
      Required: bool
      /// The type of the parameter. Unlike the corresponding swagger field, this contains the Schema Object if 'in' is of type Body.
      Type: DefinitionPropertyType
      /// Determines the format of the array if type array is used.
      CollectionFormat: CollectionFormat}

    /// Parses the Json value as an OperationParameter.
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

/// Describes a single response from an API Operation.
/// http://swagger.io/specification/#responseObject
type OperationResponse =
    { /// HTTP status code
      StatusCode: int option
      /// A short description of the response.
      Description: string
      /// A definition of the response structure. It can be a primitive, an array or an object. If this field does not exist, it means no content is returned as part of the response.
      Schema: DefinitionPropertyType option}

    /// Parses the Json value as an OperationResponse.
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

/// Describes a single API operation on a path.
/// http://swagger.io/specification/#operationObject
type OperationObject =
    { /// The name of the operation.
      Path: string
      /// The type of the REST call.
      Type: OperationType
      /// A list of tags for API documentation control.
      Tags: string[]
      /// A short summary of what the operation does. This field SHOULD be less than 120 characters.
      Summary: string
      /// A verbose explanation of the operation behavior.
      Description: string
      /// Unique string used to identify the operation.
      OperationId: string
      /// A list of MIME types the operation can consume.
      Consumes: string[]
      /// A list of MIME types the operation can produce.
      Produces: string[]
      /// The nonempty list of possible responses as they are returned from executing this operation.
      Responses: OperationResponse[]
      /// A list of parameters that are applicable for this operation. The list MUST NOT include duplicated parameters.
      Parameters: OperationParameter[]}

    /// Parses the Json value as an OperationObject.
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

/// The property of a data type.
type DefinitionProperty =
    { /// The name of the property.
      Name: string
      /// The type of the property.
      Type: DefinitionPropertyType
      /// True if the property is required.
      IsRequired : bool
      /// A description of the property.
      Description: string}

    static member Parse (name, obj, required) =
        {
            Name = name;
            Type = DefinitionPropertyType.Parse obj
            IsRequired = required
            Description = obj.GetString("description")
        }

/// A data type produced or consumed by operations.
type Definition =
    { /// Name of the data type.
      Name: string
      /// The data types properties.
      Properties: DefinitionProperty[] }

    /// Parses the Json value as a Definition.
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

/// This is the main object.
/// http://swagger.io/specification/#swaggerObject
type SwaggerSchema =
    { /// Provides metadata about the API.
      Info: InfoObject
      /// The host (name or ip) serving the API.
      Host: string
      /// The base path on which the API is served, which is relative to the host.
      BasePath: string
      /// The transfer protocol of the API. Values MUST be from the list: "http", "https", "ws", "wss". (Only the first element of the list will be used)
      Schemes: string[]
      /// A list of all operations.
      Operations: OperationObject[] // paths
      /// An object to hold data types produced and consumed by operations.
      Definitions: Definition[]
      /// A list of tags used by the specification with additional metadata.
      Tags: TagObject[]}

    /// Parses the Json value as a SwaggerSchema.
    static member Parse (obj:JsonValue) =
        let parseOperation (obj:JsonValue) path prop opType =
            obj.TryGetProperty prop
            |> Option.map (fun value->
                OperationObject.Parse(path, opType, value))
        if (obj?swagger.AsString() <> "2.0") then
            failwith "Swagger version must be 2.0"
        {
            Info = InfoObject.Parse(obj?info)
            Host = obj.GetString("host")
            BasePath = obj.GetString("basePath")
            Schemes = obj.GetStringArray("schemes")
            Tags =
                try obj?tags.AsArray() |> Array.map TagObject.Parse
                with | Failure _ -> [||]
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