namespace SwaggerProvider.Internal.Schema.Parsers

open FSharp.Data
open System

/// Schema object does not contain the `field` that is Required in Swagger specification.
type FieldNotFoundException(obj:JsonValue, field:string, specLink:string) =
    inherit Exception(
        sprintf "Object MUST contain field `%s` (See %s for more details).\nObject:%A"
            field specLink obj)

/// The `field` value is not specified in Swagger specification
type UnknownFieldValueException(obj:JsonValue, value:string, field:string, specLink:string) =
    inherit Exception(
        sprintf "Value `%s` is not allowed for field `%s`(See %s for more details).\nObject:%A"
            value field specLink obj)
