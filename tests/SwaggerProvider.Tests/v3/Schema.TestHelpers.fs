[<AutoOpen>]
module SwaggerProvider.Tests.v3_Schema_TestHelpers

open System
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.v3.Compilers

/// Parse and compile a full OpenAPI v3 schema string, then return the .NET type of
/// the `Value` property on the `TestType` component schema.
let compileSchemaAndGetValueType(schemaStr: string) : Type =
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

    let defCompiler = DefinitionCompiler(schema, false, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)

    let types = defCompiler.Namespace.GetProvidedTypes()
    let testType = types |> List.find(fun t -> t.Name = "TestType")

    match testType.GetDeclaredProperty("Value") with
    | null -> failwith "Property 'Value' not found on TestType"
    | prop -> prop.PropertyType

/// Compile a minimal v3 schema where TestType.Value is defined by `propYaml`.
let compilePropertyType (propYaml: string) (required: bool) : Type =
    let requiredBlock =
        if required then
            "      required:\n        - Value\n"
        else
            ""

    let schemaStr =
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

    compileSchemaAndGetValueType schemaStr
