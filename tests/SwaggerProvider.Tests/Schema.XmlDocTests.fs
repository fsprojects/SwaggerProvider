module SwaggerProvider.Tests.Schema_XmlDocTests

open System
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.Compilers
open Xunit
open FsUnitTyped

/// Compile a minimal OpenAPI v3 schema and return the XmlDoc string for the "Value" property
/// of "TestType", or None if no XmlDoc was added.
let private getPropertyXmlDoc(propYaml: string) : string option =
    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: XmlDocTest
  version: "1.0.0"
paths: {}
components:
  schemas:
    TestType:
      type: object
      properties:
        Value:
%s"""
            propYaml

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
    | prop ->
        prop.GetCustomAttributesData()
        |> Seq.tryFind(fun a -> a.AttributeType.Name = "TypeProviderXmlDocAttribute")
        |> Option.map(fun a -> a.ConstructorArguments.[0].Value :?> string)

// ── Property description ─────────────────────────────────────────────────────

[<Fact>]
let ``description without enum is preserved``() =
    let doc =
        getPropertyXmlDoc "          type: string\n          description: \"My description\"\n"

    doc |> shouldEqual(Some "My description")

[<Fact>]
let ``no XmlDoc added when no description and no enum``() =
    let doc = getPropertyXmlDoc "          type: string\n"
    doc |> shouldEqual None

// ── Enum values in XmlDoc ────────────────────────────────────────────────────

[<Fact>]
let ``string enum values appear in property XmlDoc``() =
    let propYaml =
        "          type: string\n          enum:\n            - active\n            - inactive\n            - pending\n"

    let doc = getPropertyXmlDoc propYaml
    doc.IsSome |> shouldEqual true
    doc.Value |> shouldContainText "Allowed values:"
    doc.Value |> shouldContainText "active"
    doc.Value |> shouldContainText "inactive"
    doc.Value |> shouldContainText "pending"

[<Fact>]
let ``integer enum values appear in property XmlDoc``() =
    let propYaml =
        "          type: integer\n          enum:\n            - 1\n            - 2\n            - 3\n"

    let doc = getPropertyXmlDoc propYaml
    doc.IsSome |> shouldEqual true
    doc.Value |> shouldContainText "Allowed values:"
    doc.Value |> shouldContainText "1"

[<Fact>]
let ``description is preserved alongside enum values``() =
    let propYaml =
        "          type: string\n          description: \"The status field\"\n          enum:\n            - active\n            - inactive\n"

    let doc = getPropertyXmlDoc propYaml
    doc.IsSome |> shouldEqual true
    doc.Value |> shouldContainText "The status field"
    doc.Value |> shouldContainText "Allowed values:"
    doc.Value |> shouldContainText "active"
    doc.Value |> shouldContainText "inactive"
