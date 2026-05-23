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
    // Map types are left unwrapped (not option) for non-required properties;
    // collection types naturally express absence via null/empty.
    let propType = dataProp.PropertyType
    propType |> shouldEqual typeof<Map<string, string>>

// ── oneOf / anyOf with multiple $refs → name alias (no wrapper type) ─────────

/// OpenAPI 3.0 schema where Union uses oneOf with two $refs and no own properties.
/// Because OneOf.Count <> 1, the single-$ref collapse guard does not fire.
/// compileNewObject() then marks Union as a name alias and returns typeof<obj>, so no wrapper type is emitted.
let private oneOfMultiRefSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Cat": {
        "type": "object",
        "properties": { "meow": { "type": "string" } }
      },
      "Dog": {
        "type": "object",
        "properties": { "bark": { "type": "string" } }
      },
      "Union": {
        "oneOf": [
          { "$ref": "#/components/schemas/Cat" },
          { "$ref": "#/components/schemas/Dog" }
        ]
      }
    }
  }
}"""

[<Fact>]
let ``oneOf with multiple refs does not emit a named wrapper type``() =
    // With no own properties and no allOf, compileNewObject() marks Union as a name alias
    // and returns typeof<obj> — no separate named type is emitted for the wrapper.
    let types = compileV3Schema oneOfMultiRefSchema false
    types |> List.exists(fun t -> t.Name = "Union") |> shouldEqual false

[<Fact>]
let ``oneOf with multiple refs emits the referenced component types``() =
    let types = compileV3Schema oneOfMultiRefSchema false
    types |> List.exists(fun t -> t.Name = "Cat") |> shouldEqual true
    types |> List.exists(fun t -> t.Name = "Dog") |> shouldEqual true

/// OpenAPI 3.0 schema where AnyUnion uses anyOf with two $refs.
let private anyOfMultiRefSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Fish": {
        "type": "object",
        "properties": { "fins": { "type": "integer" } }
      },
      "Bird": {
        "type": "object",
        "properties": { "wings": { "type": "integer" } }
      },
      "AnyUnion": {
        "anyOf": [
          { "$ref": "#/components/schemas/Fish" },
          { "$ref": "#/components/schemas/Bird" }
        ]
      }
    }
  }
}"""

[<Fact>]
let ``anyOf with multiple refs does not emit a named wrapper type``() =
    // Same behaviour as oneOf multi-ref: no own properties → compileNewObject() marks
    // AnyUnion as a name alias and returns typeof<obj>.
    let types = compileV3Schema anyOfMultiRefSchema false

    types
    |> List.exists(fun t -> t.Name = "AnyUnion")
    |> shouldEqual false

[<Fact>]
let ``anyOf with multiple refs emits the referenced component types``() =
    let types = compileV3Schema anyOfMultiRefSchema false
    types |> List.exists(fun t -> t.Name = "Fish") |> shouldEqual true
    types |> List.exists(fun t -> t.Name = "Bird") |> shouldEqual true

// ── $ref to enum type → property uses the named enum type ────────────────────

/// OpenAPI 3.0 schema where Task.status references Status (a string enum) via $ref.
let private refToEnumSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Status": {
        "type": "string",
        "enum": ["open", "closed"]
      },
      "Task": {
        "type": "object",
        "required": ["status"],
        "properties": {
          "status": { "$ref": "#/components/schemas/Status" }
        }
      }
    }
  }
}"""

[<Fact>]
let ``required property referencing a string enum uses the named enum type``() =
    let types = compileV3Schema refToEnumSchema false
    let statusType = types |> List.find(fun t -> t.Name = "Status")
    let taskType = types |> List.find(fun t -> t.Name = "Task")
    let statusProp = taskType.GetDeclaredProperty("Status")
    statusProp |> isNull |> shouldEqual false
    statusProp.PropertyType |> shouldEqual statusType

[<Fact>]
let ``required property referencing a string enum is not wrapped in option``() =
    // Required $ref to enum: the property should use the enum type directly (not Option<EnumType>).
    let types = compileV3Schema refToEnumSchema false
    let statusType = types |> List.find(fun t -> t.Name = "Status")
    let taskType = types |> List.find(fun t -> t.Name = "Task")
    let statusProp = taskType.GetDeclaredProperty("Status")
    statusProp.PropertyType |> shouldEqual statusType
    statusProp.PropertyType.IsGenericType |> shouldEqual false

// ── allOf single $ref with extra wrapper properties → new object type ─────────

/// OpenAPI 3.0 schema where Extended wraps Base via allOf with a single $ref,
/// but also declares its own extra property.  The single-$ref collapse guard
/// requires Properties.Count = 0; here it has extra properties, so it fires
/// compileNewObject() instead and emits Extended as a new object type.
let private allOfSingleRefWithExtraPropsSchema =
    """{
  "openapi": "3.0.0",
  "info": { "title": "Test", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "Base": {
        "type": "object",
        "properties": {
          "id": { "type": "integer" }
        }
      },
      "Extended": {
        "allOf": [ { "$ref": "#/components/schemas/Base" } ],
        "properties": {
          "extra": { "type": "string" }
        }
      }
    }
  }
}"""

[<Fact>]
let ``allOf single ref with extra wrapper properties emits a new named type``() =
    // Because Extended has its own properties, the collapse guard fails and a new object type is emitted.
    let types = compileV3Schema allOfSingleRefWithExtraPropsSchema false
    types |> List.exists(fun t -> t.Name = "Extended") |> shouldEqual true

[<Fact>]
let ``allOf single ref with extra wrapper properties emits the extra property``() =
    let types = compileV3Schema allOfSingleRefWithExtraPropsSchema false
    let extendedType = types |> List.find(fun t -> t.Name = "Extended")

    extendedType.GetDeclaredProperty("Extra")
    |> isNull
    |> shouldEqual false
