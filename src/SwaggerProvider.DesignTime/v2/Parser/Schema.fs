namespace SwaggerProvider.Internal.v2.Parser.Schema

/// A data type produced or consumed by operations.
/// http://swagger.io/specification/#schemaObject
type SchemaObject =
    /// Boolean.
    | Boolean
    /// Byte - we need this to support byte[] transfered as base64 encoded string
    | Byte
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
    /// An additional primitive data type used by the Parameter Object and the Response Object to set the parameter type or the response as being a file.
    | File
    /// Enumeration
    | Enum of values: string[]
    /// Array of items of type itemTy
    | Array of itemTy: SchemaObject
    /// Object
    | Object of DefinitionProperty[]
    /// Dictionary
    | Dictionary of ty: SchemaObject
    /// A reference to an object defined by a Schema Object.
    | Reference of name: string

    override this.ToString() =
        match this with
        | Object _ -> "Object"
        | Boolean -> "Boolean"
        | Byte -> "Byte"
        | Int32 -> "Int32"
        | Int64 -> "Int64"
        | Float -> "Float"
        | Double -> "Double"
        | String -> "String"
        | Date -> "Date"
        | DateTime -> "DateTime"
        | Enum x -> sprintf "Enum  %A" x
        | Array x -> sprintf "Array %O" x
        | Dictionary x -> sprintf "Dictionary %O" x
        | File -> "File"
        | Reference s -> sprintf "Reference %s" s


/// The property of a data type.
and DefinitionProperty = {
    /// The name of the property.
    Name: string
    /// The type of the property.
    Type: SchemaObject
    /// True if the property is required.
    IsRequired: bool
    /// A description of the property.
    Description: string
}


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
        | Get -> "GET"
        | Put -> "PUT"
        | Post -> "POST"
        | Delete -> "DELETE"
        | Options -> "OPTIONS"
        | Head -> "HEAD"
        | Patch -> "PATCH"


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
        | Csv -> ","
        | Ssv -> " "
        | Tsv -> "\t"
        | Pipes -> "|"
        | Multi -> failwith "CollectionFormat 'Multi' does not support ToString()"


/// Required. The location of the parameter.
type ParameterObjectLocation =
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


/// Describes a single operation parameter.
/// http://swagger.io/specification/#parameterObject
type ParameterObject =
    {
        /// Required. The name of the parameter. Parameter names are case sensitive.
        /// If in is "path", the name field MUST correspond to the associated path segment from the path field in the Paths Object. See Path Templating for further information.
        /// For all other cases, the name corresponds to the parameter name used based on the in property.
        Name: string
        /// Required. The location of the parameter.
        In: ParameterObjectLocation
        /// A brief description of the parameter. This could contain examples of use.
        Description: string
        /// Determines whether this parameter is mandatory. If the parameter is in "path", this property is required and its value MUST be true. Otherwise, the property MAY be included and its default value is false.
        Required: bool
        /// The type of the parameter. Unlike the corresponding swagger field, this contains the Schema Object if 'in' is of type Body.
        Type: SchemaObject
        /// Determines the format of the array if type array is used.
        CollectionFormat: CollectionFormat
    }
    member x.UnambiguousName = sprintf "%sIn%A" x.Name x.In


/// Describes a single response from an API Operation.
/// http://swagger.io/specification/#responseObject
type ResponseObject = {
    /// Required. A short description of the response.
    Description: string
    /// A definition of the response structure. It can be a primitive, an array or an object. If this field does not exist, it means no content is returned as part of the response.
    Schema: SchemaObject option
}


/// Describes a single API operation on a path.
/// http://swagger.io/specification/#operationObject
type OperationObject = {
    /// The name of the operation.
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
    /// Required. The nonempty list of possible status codes and responses as they are returned from executing this operation.
    Responses: (Option<int> * ResponseObject)[]
    /// A list of parameters that are applicable for this operation. The list MUST NOT include duplicated parameters.
    Parameters: ParameterObject[]
    /// Declares this operation to be deprecated.
    Deprecated: bool
}


/// Basic swagger information, relevant to the type provider.
/// http://swagger.io/specification/#infoObject
type InfoObject = {
    /// Required. The title of the application.
    Title: string
    /// A short description of the application.
    Description: string
    /// Required. Provides the version of the application API (not to be confused with the specification version).
    Version: string
}


/// Allows adding meta data to a single tag.
/// http://swagger.io/specification/#tagObject
type TagObject = {
    /// Required. The name of the tag.
    Name: string
    /// A short description for the tag.
    Description: string
}


/// This is the main object.
/// http://swagger.io/specification/#swaggerObject
type SwaggerObject = {
    /// Required. Provides metadata about the API.
    Info: InfoObject
    /// The host (name or ip) serving the API.
    Host: string
    /// The base path on which the API is served, which is relative to the host.
    BasePath: string
    /// The transfer protocol of the API. Values MUST be from the list: "http", "https", "ws", "wss". (Only the first element of the list will be used)
    Schemes: string[]
    /// Required. A list of all operations.
    Paths: OperationObject[]
    /// An object to hold data types produced and consumed by operations.
    Definitions: (string * SchemaObject)[]
    /// A list of tags used by the specification with additional metadata.
    Tags: TagObject[]
}
