namespace SwaggerProvider.Internal.v2.Parser

open System

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
