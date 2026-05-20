module SwaggerProvider.Tests.Schema_V2SchemaCompilationTests

/// Tests that verify Swagger 2.0 schemas can be parsed and compiled by the
/// v3 DefinitionCompiler and OperationCompiler pipeline via Microsoft.OpenApi.
/// Microsoft.OpenApi normalises both v2 and v3 documents into the same
/// OpenApiDocument representation, so the v3 compiler pipeline works for both.
/// These tests document and protect this behaviour as the maintainer intends
/// OpenApiClientProvider to become the single supported provider for v2 and v3.

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.OpenApi.Reader
open SwaggerProvider.Internal.Compilers
open Xunit
open FsUnitTyped

/// Parse a Swagger 2.0 JSON schema and compile it with the v3 compiler pipeline.
/// Returns the compiled provided types as a ProvidedTypeDefinition list.
let private compileV2Schema(jsonSchema: string) : ProviderImplementation.ProvidedTypes.ProvidedTypeDefinition list =
    let settings = OpenApiReaderSettings()
    settings.AddYamlReader()

    let readResult =
        Microsoft.OpenApi.OpenApiDocument.Parse(jsonSchema, settings = settings)

    match readResult.Diagnostic with
    | null -> ()
    | diagnostic when diagnostic.Errors |> Seq.isEmpty |> not ->
        let errorText =
            diagnostic.Errors
            |> Seq.map string
            |> String.concat Environment.NewLine

        failwithf "Failed to parse v2 schema:%s%s" Environment.NewLine errorText
    | _ -> ()

    let schema =
        match readResult.Document with
        | null -> failwith "Failed to parse v2 schema: Document is null."
        | doc -> doc

    let defCompiler = DefinitionCompiler(schema, false, false)
    let opCompiler = OperationCompiler(schema, defCompiler, true, false, false)
    opCompiler.CompileProvidedClients(defCompiler.Namespace)
    defCompiler.Namespace.GetProvidedTypes()

let private getProp (t: ProviderImplementation.ProvidedTypes.ProvidedTypeDefinition) (name: string) =
    match t.GetDeclaredProperty(name) with
    | null -> failwithf "Property '%s' not found on type '%s'" name t.Name
    | prop -> prop

let private minimalPetstoreV2 =
    """{
  "swagger": "2.0",
  "info": { "title": "Petstore", "version": "1.0.0" },
  "basePath": "/api",
  "host": "example.com",
  "paths": {
    "/pets": {
      "get": {
        "operationId": "listPets",
        "summary": "List all pets",
        "produces": ["application/json"],
        "parameters": [],
        "responses": {
          "200": {
            "description": "A list of pets",
            "schema": { "type": "array", "items": { "$ref": "#/definitions/Pet" } }
          }
        }
      },
      "post": {
        "operationId": "createPet",
        "summary": "Create a pet",
        "consumes": ["application/json"],
        "parameters": [
          {
            "in": "body",
            "name": "body",
            "schema": { "$ref": "#/definitions/NewPet" }
          }
        ],
        "responses": { "201": { "description": "Pet created" } }
      }
    },
    "/pets/{id}": {
      "get": {
        "operationId": "getPet",
        "summary": "Get a pet by ID",
        "parameters": [
          { "in": "path", "name": "id", "required": true, "type": "integer", "format": "int64" }
        ],
        "responses": {
          "200": {
            "description": "The pet",
            "schema": { "$ref": "#/definitions/Pet" }
          }
        }
      }
    }
  },
  "definitions": {
    "Pet": {
      "type": "object",
      "required": ["id", "name"],
      "properties": {
        "id": { "type": "integer", "format": "int64" },
        "name": { "type": "string" },
        "tag": { "type": "string" }
      }
    },
    "NewPet": {
      "type": "object",
      "required": ["name"],
      "properties": {
        "name": { "type": "string" },
        "tag": { "type": "string" }
      }
    }
  }
}"""

[<Fact>]
let ``v2 petstore schema compiles without exception``() =
    let types = compileV2Schema minimalPetstoreV2
    types |> List.isEmpty |> shouldEqual false

[<Fact>]
let ``v2 petstore schema generates Pet definition type``() =
    let types = compileV2Schema minimalPetstoreV2
    let typeNames = types |> List.map(fun t -> t.Name)
    typeNames |> shouldContain "Pet"

[<Fact>]
let ``v2 petstore Pet type has correct property types``() =
    let types = compileV2Schema minimalPetstoreV2
    let petType = types |> List.find(fun t -> t.Name = "Pet")
    let idProp = getProp petType "Id"
    let nameProp = getProp petType "Name"
    let tagProp = getProp petType "Tag"
    // required int64
    idProp.PropertyType |> shouldEqual typeof<int64>
    // required string
    nameProp.PropertyType |> shouldEqual typeof<string>
    // optional string — now wrapped in Option<string>
    tagProp.PropertyType |> shouldEqual typeof<string option>

[<Fact>]
let ``v2 petstore schema generates API client types with operations``() =
    let types = compileV2Schema minimalPetstoreV2
    // OperationCompiler generates at least one client class with methods
    let hasOperations =
        types
        |> List.exists(fun t ->
            let methods = t.GetMethods()

            methods
            |> Array.exists(fun m -> m.Name = "ListPets" || m.Name = "GetPet" || m.Name = "CreatePet"))

    hasOperations |> shouldEqual true

[<Fact>]
let ``v2 schema with integer enum property compiles``() =
    let schema =
        """{
  "swagger": "2.0",
  "info": { "title": "EnumTest", "version": "1.0.0" },
  "basePath": "/",
  "paths": {},
  "definitions": {
    "Status": {
      "type": "object",
      "required": ["code"],
      "properties": {
        "code": { "type": "integer", "enum": [1, 2, 3] }
      }
    }
  }
}"""

    let types = compileV2Schema schema
    let statusType = types |> List.find(fun t -> t.Name = "Status")
    let codeProp = getProp statusType "Code"
    // integer enum — Microsoft.OpenApi maps this to the integer base type
    codeProp.PropertyType |> shouldEqual typeof<int32>

// ── ToString tests ───────────────────────────────────────────────────────────

[<Fact>]
let ``v2 compiled object type declares ToString override``() =
    let types = compileV2Schema minimalPetstoreV2
    let petType = types |> List.find(fun t -> t.Name = "Pet")

    let toStr =
        petType.GetMethods(
            BindingFlags.Public
            ||| BindingFlags.Instance
            ||| BindingFlags.DeclaredOnly
        )
        |> Array.tryFind(fun m -> m.Name = "ToString" && m.GetParameters().Length = 0)

    toStr.IsSome |> shouldEqual true
    toStr.Value.ReturnType |> shouldEqual typeof<string>

[<Fact>]
let ``v2 compiled object type ToString invokeCode does not throw for concrete provided type``() =
    // This test guards against the FS3033 regression where splicing a concrete provided-type
    // expression into a quotation expecting obj caused "Type mismatch when splicing expression".
    let types = compileV2Schema minimalPetstoreV2
    let petType = types |> List.find(fun t -> t.Name = "Pet")

    let toStr =
        petType.GetMethods(
            BindingFlags.Public
            ||| BindingFlags.Instance
            ||| BindingFlags.DeclaredOnly
        )
        |> Array.find(fun m -> m.Name = "ToString" && m.GetParameters().Length = 0)

    let providedMethod = toStr :?> ProviderImplementation.ProvidedTypes.ProvidedMethod

    // Access GetInvokeCode via reflection to work around internal accessibility
    let invokeCodeProp =
        providedMethod.GetType().GetProperty("GetInvokeCode", BindingFlags.Instance ||| BindingFlags.NonPublic)

    if isNull invokeCodeProp then
        failwith "GetInvokeCode property not found on ProvidedMethod"

    let invokeCodeOpt =
        invokeCodeProp.GetValue(providedMethod) :?> (Expr list -> Expr) option

    match invokeCodeOpt with
    | None -> failwith "ToString has no invoke code"
    | Some invokeCode ->
        let thisVar = Var("this", petType)
        let thisExpr = Expr.Var thisVar
        // Must not throw; original bug threw FS3033 here because %%this was
        // typed as FileDescription/Pet rather than obj inside the quotation body
        let body = invokeCode [ thisExpr ]
        // Expr is a value type; just verifying invokeCode did not throw is sufficient
        body.Type |> shouldEqual typeof<string>
