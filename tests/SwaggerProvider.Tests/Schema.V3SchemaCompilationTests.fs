module SwaggerProvider.Tests.Schema_V3SchemaCompilationTests

/// Tests for OpenAPI 3.0-specific schema compilation behaviour in the v3
/// DefinitionCompiler pipeline.

open Xunit
open FsUnitTyped

// ── allOf/oneOf/anyOf single-$ref wrapper collapse ────────────────────────────

// Tests for the DefinitionCompiler's explicit allOf/oneOf/anyOf single-$ref collapse
// branches (DefinitionCompiler.fs lines 470-502), which release the name reservation
// for the wrapper schema and return the already-compiled referenced type directly.
// This avoids emitting an empty wrapper type when a schema has no explicit properties
// and carries a single $ref inside allOf/oneOf/anyOf.

/// Minimal OpenAPI 3.0 document with a Pet schema and a PetRef schema that wraps
/// it via allOf with a single $ref. The compiler should collapse PetRef into Pet
/// rather than creating a new empty object type.
let private allOfSingleRefSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Pet": {
        "type": "object",
        "properties": {
          "id": { "type": "integer" },
          "name": { "type": "string" }
        }
      },
      "PetRef": {
        "allOf": [ { "$ref": "#/components/schemas/Pet" } ]
      }
    }
  }
}"""

/// Same schema but using oneOf instead of allOf.
let private oneOfSingleRefSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Dog": {
        "type": "object",
        "properties": { "breed": { "type": "string" } }
      },
      "DogRef": {
        "oneOf": [ { "$ref": "#/components/schemas/Dog" } ]
      }
    }
  }
}"""

/// Same schema but using anyOf instead of allOf.
let private anyOfSingleRefSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Cat": {
        "type": "object",
        "properties": { "color": { "type": "string" } }
      },
      "CatRef": {
        "anyOf": [ { "$ref": "#/components/schemas/Cat" } ]
      }
    }
  }
}"""

[<Fact>]
let ``allOf single $ref resolves to the referenced type without creating a new object type``() =
    let types = compileV3Schema allOfSingleRefSchema false
    // PetRef collapses into Pet via ReleaseNameReservation; the referenced type is present.
    types |> List.exists(fun t -> t.Name = "Pet") |> shouldEqual true

[<Fact>]
let ``allOf single $ref does not produce a separate wrapper type``() =
    let types = compileV3Schema allOfSingleRefSchema false
    // PetRef's name reservation is released; no separate empty type named "PetRef" is emitted.
    types |> List.exists(fun t -> t.Name = "PetRef") |> shouldEqual false

[<Fact>]
let ``oneOf single $ref resolves to the referenced type``() =
    let types = compileV3Schema oneOfSingleRefSchema false
    types |> List.exists(fun t -> t.Name = "Dog") |> shouldEqual true

[<Fact>]
let ``oneOf single $ref does not produce a separate wrapper type``() =
    let types = compileV3Schema oneOfSingleRefSchema false
    types |> List.exists(fun t -> t.Name = "DogRef") |> shouldEqual false

[<Fact>]
let ``anyOf single $ref resolves to the referenced type``() =
    let types = compileV3Schema anyOfSingleRefSchema false
    types |> List.exists(fun t -> t.Name = "Cat") |> shouldEqual true

[<Fact>]
let ``anyOf single $ref does not produce a separate wrapper type``() =
    let types = compileV3Schema anyOfSingleRefSchema false
    types |> List.exists(fun t -> t.Name = "CatRef") |> shouldEqual false
