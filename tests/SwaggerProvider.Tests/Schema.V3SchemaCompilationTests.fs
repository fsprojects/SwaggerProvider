module SwaggerProvider.Tests.Schema_V3SchemaCompilationTests

/// Tests for OpenAPI 3.0-specific schema compilation behaviour in the v3
/// DefinitionCompiler pipeline.

open Xunit
open FsUnitTyped

// ── allOf/oneOf/anyOf single-$ref wrapper collapse ────────────────────────────

// Tests for the DefinitionCompiler's explicit allOf/oneOf/anyOf single-$ref collapse
// branches, which release the name reservation for the wrapper schema and return
// the already-compiled referenced type directly via ReleaseNameReservation.
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

// ── Required vs optional properties ──────────────────────────────────────────

[<Fact>]
let ``required property compiles to non-option type``() =
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Order": {
        "type": "object",
        "required": ["id"],
        "properties": {
          "id":   { "type": "integer" },
          "note": { "type": "string" }
        }
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    let orderType = types |> List.find(fun t -> t.Name = "Order")
    // 'id' is required → int32 (not option)
    orderType.GetDeclaredProperty("Id").PropertyType
    |> shouldEqual typeof<int32>

[<Fact>]
let ``optional property compiles to option type``() =
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Order": {
        "type": "object",
        "required": ["id"],
        "properties": {
          "id":   { "type": "integer" },
          "note": { "type": "string" }
        }
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    let orderType = types |> List.find(fun t -> t.Name = "Order")
    // 'note' is not required → string option
    orderType.GetDeclaredProperty("Note").PropertyType
    |> shouldEqual typeof<string option>

// ── String enum compilation ────────────────────────────────────────────────────

[<Fact>]
let ``string enum schema compiles to a named enum type``() =
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Status": {
        "type": "string",
        "enum": ["active", "inactive", "pending"]
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    types |> List.exists(fun t -> t.Name = "Status") |> shouldEqual true

[<Fact>]
let ``string enum type is an enum``() =
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Status": {
        "type": "string",
        "enum": ["active", "inactive", "pending"]
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    let statusType = types |> List.find(fun t -> t.Name = "Status")
    statusType.IsEnum |> shouldEqual true

// ── Schema description as XmlDoc ─────────────────────────────────────────────

[<Fact>]
let ``object schema description is surfaced as XmlDoc``() =
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Widget": {
        "type": "object",
        "description": "A widget with a name",
        "properties": {
          "name": { "type": "string" }
        }
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    let widgetType = types |> List.find(fun t -> t.Name = "Widget")
    // XmlDoc is accessible via GetCustomAttributesData on the provided type
    let doc = widgetType.GetCustomAttributesData()

    doc
    |> Seq.exists(fun a ->
        a.AttributeType.Name = "TypeProviderXmlDocAttribute"
        && a.ConstructorArguments.Count > 0
        && a.ConstructorArguments.[0].Value :? string
        && (a.ConstructorArguments.[0].Value :?> string).Contains("A widget with a name"))
    |> shouldEqual true

// ── allOf composite with multiple inline schemas ──────────────────────────────

/// OpenAPI 3.0 schema where Dog uses allOf to merge two inline objects.
/// Both inline schemas contribute properties; the compiler should emit all of them.
let private allOfCompositeSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Dog": {
        "type": "object",
        "allOf": [
          {
            "type": "object",
            "properties": {
              "name": { "type": "string" }
            }
          },
          {
            "type": "object",
            "properties": {
              "breed": { "type": "string" }
            }
          }
        ]
      }
    }
  }
}"""

[<Fact>]
let ``allOf composite with multiple inline schemas emits all merged properties``() =
    let types = compileV3Schema allOfCompositeSchema false
    let dogType = types |> List.find(fun t -> t.Name = "Dog")
    dogType.GetDeclaredProperty("Name") |> isNull |> shouldEqual false
    dogType.GetDeclaredProperty("Breed") |> isNull |> shouldEqual false

[<Fact>]
let ``allOf composite merged properties have correct types``() =
    let types = compileV3Schema allOfCompositeSchema false
    let dogType = types |> List.find(fun t -> t.Name = "Dog")

    dogType.GetDeclaredProperty("Name").PropertyType
    |> shouldEqual typeof<string option>

    dogType.GetDeclaredProperty("Breed").PropertyType
    |> shouldEqual typeof<string option>

// ── nullable required property → option type ─────────────────────────────────

[<Fact>]
let ``required nullable property compiles to option type``() =
    // In OpenAPI 3.0, a required + nullable property must be Option<T>
    // because the value may be present but null.
    let schema =
        """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Status": {
        "type": "object",
        "required": ["code"],
        "properties": {
          "code": { "type": "string", "nullable": true }
        }
      }
    }
  }
}"""

    let types = compileV3Schema schema false
    let statusType = types |> List.find(fun t -> t.Name = "Status")

    statusType.GetDeclaredProperty("Code").PropertyType
    |> shouldEqual typeof<string option>

// ── additionalProperties → Map<string, T> ────────────────────────────────────

/// OpenAPI 3.0 schema where StringMap has only additionalProperties (no explicit properties).
/// The compiler releases the name reservation and compiles it to Map<string, string>.
/// Any property referencing StringMap by $ref should receive type Map<string, string>.
let private additionalPropertiesSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "StringMap": {
        "type": "object",
        "additionalProperties": { "type": "string" }
      },
      "Wrapper": {
        "type": "object",
        "properties": {
          "data": { "$ref": "#/components/schemas/StringMap" }
        }
      }
    }
  }
}"""

[<Fact>]
let ``schema with only additionalProperties does not emit a named type``() =
    let types = compileV3Schema additionalPropertiesSchema false
    // StringMap's name reservation is released; no separate named type is emitted
    types
    |> List.exists(fun t -> t.Name = "StringMap")
    |> shouldEqual false

[<Fact>]
let ``property referencing an additionalProperties schema has Map type``() =
    let types = compileV3Schema additionalPropertiesSchema false
    let wrapperType = types |> List.find(fun t -> t.Name = "Wrapper")
    let dataProp = wrapperType.GetDeclaredProperty("Data")
    dataProp |> isNull |> shouldEqual false
    // Map<string, string> is optional because Wrapper.data is not required
    let propType = dataProp.PropertyType
    propType |> shouldEqual typeof<Map<string, string> option>
