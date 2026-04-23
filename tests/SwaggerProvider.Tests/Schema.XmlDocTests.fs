module SwaggerProvider.Tests.Schema_XmlDocTests

open System
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.Compilers
open Xunit
open FsUnitTyped

let private parseSchema(schemaStr: string) =
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

    match readResult.Document with
    | null -> failwith "Failed to parse OpenAPI schema: Document is null."
    | doc -> doc

let private getXmlDocAttr(m: System.Reflection.MemberInfo) =
    m.GetCustomAttributesData()
    |> Seq.tryFind(fun a -> a.AttributeType.Name = "TypeProviderXmlDocAttribute")
    |> Option.map(fun a -> a.ConstructorArguments.[0].Value :?> string)

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

    let schema = parseSchema schemaStr

    let defCompiler = DefinitionCompiler(schema, false, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)

    let types = defCompiler.Namespace.GetProvidedTypes()
    let testType = types |> List.find(fun t -> t.Name = "TestType")

    match testType.GetDeclaredProperty("Value") with
    | null -> failwith "Property 'Value' not found on TestType"
    | prop -> getXmlDocAttr prop

/// Compile a minimal OpenAPI v3 schema and return the XmlDoc string for the generated
/// operation method, or None if no XmlDoc was added.
let private getMethodXmlDoc (pathsYaml: string) (operationId: string) : string option =
    let schemaStr =
        sprintf
            """openapi: "3.0.0"
info:
  title: XmlDocMethodTest
  version: "1.0.0"
paths:
%s
components:
  schemas: {}
"""
            pathsYaml

    let schema = parseSchema schemaStr

    let defCompiler = DefinitionCompiler(schema, false, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, true)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)

    let types = defCompiler.Namespace.GetProvidedTypes()

    types
    |> List.collect(fun t -> t.GetMethods() |> Array.toList)
    |> List.tryFind(fun m -> m.Name.Equals(operationId, StringComparison.OrdinalIgnoreCase))
    |> Option.bind getXmlDocAttr

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

// ── Enum values in property XmlDoc ────────────────────────────────────────────

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

// ── Enum values in operation parameter XmlDoc ─────────────────────────────────

let private statusEnumParamSchema =
    """  /items:
    get:
      operationId: listItems
      summary: List items
      parameters:
        - name: status
          in: query
          description: "Filter by status"
          schema:
            type: string
            enum:
              - active
              - inactive
              - pending
      responses:
        "200":
          description: OK
          content:
            application/json:
              schema:
                type: string
"""

[<Fact>]
let ``enum query parameter values appear in method XmlDoc param tag``() =
    let doc = getMethodXmlDoc statusEnumParamSchema "ListItems"
    doc.IsSome |> shouldEqual true
    doc.Value |> shouldContainText "Allowed values:"
    doc.Value |> shouldContainText "active"
    doc.Value |> shouldContainText "inactive"
    doc.Value |> shouldContainText "pending"

[<Fact>]
let ``enum parameter description and allowed values are both preserved in method XmlDoc``() =
    let doc = getMethodXmlDoc statusEnumParamSchema "ListItems"
    doc.IsSome |> shouldEqual true
    doc.Value |> shouldContainText "Filter by status"
    doc.Value |> shouldContainText "Allowed values:"

let private noEnumParamSchema =
    """  /health:
    get:
      operationId: getHealth
      summary: Health check
      parameters:
        - name: verbose
          in: query
          description: "Verbose output"
          schema:
            type: boolean
      responses:
        "200":
          description: OK
"""

[<Fact>]
let ``non-enum query parameter does not add Allowed values to XmlDoc``() =
    let doc = getMethodXmlDoc noEnumParamSchema "GetHealth"
    // There is a summary, so XmlDoc should be set, but must NOT contain "Allowed values"
    match doc with
    | None -> () // no XmlDoc at all — also fine
    | Some d -> d |> shouldNotContainText "Allowed values:"
