[<AutoOpen>]
module SwaggerProvider.Tests.v3_Schema_TestHelpers

open System
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.v3.Compilers

/// Core: parse, validate, and compile an OpenAPI v3 schema string.
/// `provideNullable` controls whether optional value-type properties use Nullable<T>.
/// `asAsync` controls whether operation return types are Async<'T> or Task<'T>.
let private compileV3SchemaCore (schemaStr: string) (provideNullable: bool) (asAsync: bool) =
    let settings = OpenApiReaderSettings()
    settings.AddYamlReader()

    let readResult =
        Microsoft.OpenApi.OpenApiDocument.Parse(schemaStr, settings = settings)

    match readResult.Diagnostic with
    | null -> ()
    | diagnostic when diagnostic.Errors |> Seq.isEmpty |> not ->
        let errorText =
            diagnostic.Errors
            |> Seq.map string
            |> String.concat Environment.NewLine

        failwithf "Failed to parse OpenAPI schema:%s%s" Environment.NewLine errorText
    | _ -> ()

    let schema =
        match readResult.Document with
        | null -> failwith "Failed to parse OpenAPI schema: Document is null."
        | doc -> doc

    let defCompiler = DefinitionCompiler(schema, provideNullable, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, asAsync)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)
    defCompiler.Namespace.GetProvidedTypes()

/// Parse and compile a full OpenAPI v3 schema string, then return all provided types.
/// Pass asAsync=true to generate Async<'T> operation return types, or false for Task<'T>.
let compileV3Schema (schemaStr: string) (asAsync: bool) =
    compileV3SchemaCore schemaStr false asAsync

/// Parse and compile a full OpenAPI v3 schema string, then return the .NET type of
/// the `Value` property on the `TestType` component schema.
let compileSchemaAndGetValueType(schemaStr: string) : Type =
    let types = compileV3Schema schemaStr false
    let testType = types |> List.find(fun t -> t.Name = "TestType")

    match testType.GetDeclaredProperty("Value") with
    | null -> failwith "Property 'Value' not found on TestType"
    | prop -> prop.PropertyType

/// Build the minimal v3 schema string for a TestType.Value property.
let private buildPropertySchema (propYaml: string) (required: bool) =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    sprintf
        """openapi: "3.0.0"
info:
  title: TypeMappingTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    TestType:
      type: object
%s      properties:
        Value:
%s"""
        requiredBlock
        propYaml

/// Compile a minimal v3 schema where TestType.Value is defined by `propYaml`.
let compilePropertyType (propYaml: string) (required: bool) : Type =
    compileSchemaAndGetValueType(buildPropertySchema propYaml required)

/// Compile a minimal v3 schema with configurable DefinitionCompiler options.
/// Returns the .NET type of the `Value` property on `TestType`.
let compilePropertyTypeWith (provideNullable: bool) (propYaml: string) (required: bool) : Type =
    let types =
        compileV3SchemaCore (buildPropertySchema propYaml required) provideNullable false

    let testType = types |> List.find(fun t -> t.Name = "TestType")

    match testType.GetDeclaredProperty("Value") with
    | null -> failwith "Property 'Value' not found on TestType"
    | prop -> prop.PropertyType
