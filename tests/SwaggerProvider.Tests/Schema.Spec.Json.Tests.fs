module Schema.Spec.Json.Tests

open SwaggerProvider.Internal.Schema
open SwaggerProvider.Internal.Schema.Parsers
open NUnit.Framework
open FsUnit
open FSharp.Data

[<Test>]
let ``Info Object Example`` () =
    """{
        "title": "Swagger Sample App",
        "description": "This is a sample server Petstore server.",
        "termsOfService": "http://swagger.io/terms/",
        "contact": {
            "name": "API Support",
            "url": "http://www.swagger.io/support",
            "email": "support@swagger.io"
        },
        "license": {
            "name": "Apache 2.0",
            "url": "http://www.apache.org/licenses/LICENSE-2.0.html"
        },
        "version": "1.0.1"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseInfoObject
    |> should equal
        {
            Title = "Swagger Sample App"
            Description = "This is a sample server Petstore server."
            Version = "1.0.1"
        }


[<Test>]
let ``Paths Object Example`` () =
    """{
        "/pets": {
        "get": {
            "description": "Returns all pets from the system that the user has access to",
            "produces": [
            "application/json"
            ],
            "responses": {
            "200": {
                "description": "A list of pets.",
                "schema": {
                "type": "array",
                "items": {
                    "$ref": "#/definitions/pet"
                }
                }
            }
            }
        }
        }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parsePathsObject Parser.ParserContext.Empty
    |> should equal
        [|{
            Path = "/pets"
            Type = Get
            Tags = [||]
            Summary = ""
            Description = "Returns all pets from the system that the user has access to"
            OperationId = ""
            Consumes = [||]
            Produces = [|"application/json"|]
            Responses =
                [|Some(200),
                    { Description = "A list of pets."
                      Schema = Some <| Array (Reference "#/definitions/pet")}|]
            Parameters = [||]
            Deprecated = false
          }|]


[<Test>]
let ``Path Item Object Example`` () =
    """{"/pets":{
      "get": {
        "description": "Returns pets based on ID",
        "summary": "Find pets by ID",
        "operationId": "getPetsById",
        "produces": [
          "application/json",
          "text/html"
        ],
        "responses": {
          "200": {
            "description": "pet response",
            "schema": {
              "type": "array",
              "items": {
                "$ref": "#/definitions/Pet"
              }
            }
          },
          "default": {
            "description": "error payload",
            "schema": {
              "$ref": "#/definitions/ErrorModel"
            }
          }
        }
      },
      "parameters": [
        {
          "name": "id",
          "in": "path",
          "description": "ID of pet to use",
          "required": true,
          "type": "array",
          "items": {
            "type": "string"
          },
          "collectionFormat": "csv"
        }
      ]
    }}"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parsePathsObject Parser.ParserContext.Empty
    |> should equal
        [|{
            Path = "/pets"
            Type = Get
            Tags = [||]
            Summary = "Find pets by ID"
            Description = "Returns pets based on ID"
            OperationId = "getPetsById"
            Consumes = [||]
            Produces = [|"application/json"; "text/html"|]
            Responses =
                [|Some(200),
                    { Description = "pet response"
                      Schema = Some <| Array (Reference "#/definitions/Pet")}
                  None,
                    { Description = "error payload"
                      Schema = Some <| Reference "#/definitions/ErrorModel"}|]
            Parameters =
                [|{
                  Name = "id"
                  In = Path
                  Description = "ID of pet to use"
                  Required = true
                  Type = Array String
                  CollectionFormat = Csv
                }|]
            Deprecated = false
          }|]


[<Test>]
let ``Operation Object Example`` () =
    """{
      "tags": [
        "pet"
      ],
      "summary": "Updates a pet in the store with form data",
      "description": "",
      "operationId": "updatePetWithForm",
      "consumes": [
        "application/x-www-form-urlencoded"
      ],
      "produces": [
        "application/json",
        "application/xml"
      ],
      "parameters": [
        {
          "name": "petId",
          "in": "path",
          "description": "ID of pet that needs to be updated",
          "required": true,
          "type": "string"
        },
        {
          "name": "name",
          "in": "formData",
          "description": "Updated name of the pet",
          "required": false,
          "type": "string"
        },
        {
          "name": "status",
          "in": "formData",
          "description": "Updated status of the pet",
          "required": false,
          "type": "string"
        }
      ],
      "responses": {
        "200": {
          "description": "Pet updated."
        },
        "405": {
          "description": "Invalid input"
        }
      },
      "security": [
        {
          "petstore_auth": [
            "write:pets",
            "read:pets"
          ]
        }
      ]
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseOperationObject Parser.ParserContext.Empty "/" Get
    |> should equal
        {
            Path = "/"
            Type = Get
            Tags = [|"pet"|]
            Summary = "Updates a pet in the store with form data"
            Description = ""
            OperationId = "updatePetWithForm"
            Consumes = [|"application/x-www-form-urlencoded"|]
            Produces = [|"application/json"; "application/xml"|]
            Responses =
                [|Some(200),
                    { Description = "Pet updated."
                      Schema = None}
                  Some(405),
                    { Description = "Invalid input"
                      Schema = None }|]
            Parameters =
                [|{
                    Name = "petId"
                    In = Path
                    Description = "ID of pet that needs to be updated"
                    Required = true
                    Type = String
                    CollectionFormat = Csv
                  }
                  {
                    Name = "name"
                    In = FormData
                    Description = "Updated name of the pet"
                    Required = false
                    Type = String
                    CollectionFormat = Csv
                  }
                  {
                    Name = "status"
                    In = FormData
                    Description = "Updated status of the pet"
                    Required = false
                    Type = String
                    CollectionFormat = Csv
                  }|]
            Deprecated = false
        }


[<Test>]
let ``Parameter Object Examples: Body Parameters``() =
    """{
      "name": "user",
      "in": "body",
      "description": "user to add to the system",
      "required": true,
      "schema": {
        "$ref": "#/definitions/User"
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "user"
            In = Body
            Description = "user to add to the system"
            Required = true
            Type = Reference "#/definitions/User"
            CollectionFormat = Csv
        }


[<Test>]
let ``Parameter Object Examples: Body Parameters Array``() =
    """{
      "name": "user",
      "in": "body",
      "description": "user to add to the system",
      "required": true,
      "schema": {
        "type": "array",
        "items": {
          "type": "string"
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "user"
            In = Body
            Description = "user to add to the system"
            Required = true
            Type = Array String
            CollectionFormat = Csv
        }


[<Test>]
let ``Parameter Object Examples: Other Parameters``() =
    """{
      "name": "token",
      "in": "header",
      "description": "token to be passed as a header",
      "required": true,
      "type": "array",
      "items": {
        "type": "integer",
        "format": "int64"
      },
      "collectionFormat": "csv"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "token"
            In = Header
            Description = "token to be passed as a header"
            Required = true
            Type = Array Int64
            CollectionFormat = Csv
        }


[<Test>]
let ``Parameter Object Examples: Other Parameters - Path String``() =
    """{
      "name": "username",
      "in": "path",
      "description": "username to fetch",
      "required": true,
      "type": "string"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "username"
            In = Path
            Description = "username to fetch"
            Required = true
            Type = String
            CollectionFormat = Csv
        }


[<Test>]
let ``Parameter Object Examples: Other Parameters - Array String Multi``() =
    """{
      "name": "id",
      "in": "query",
      "description": "ID of the object to fetch",
      "required": false,
      "type": "array",
      "items": {
        "type": "string"
      },
      "collectionFormat": "multi"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "id"
            In = Query
            Description = "ID of the object to fetch"
            Required = false
            Type = Array String
            CollectionFormat = Multi
        }


[<Test>]
let ``Parameter Object Examples: Other Parameters - File``() =
    """{
      "name": "avatar",
      "in": "formData",
      "description": "The avatar of the user",
      "required": true,
      "type": "file"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParameterObject
    |> should equal
        {
            Name = "avatar"
            In = FormData
            Description = "The avatar of the user"
            Required = true
            Type = File
            CollectionFormat = Csv
        }


[<Test>]
let ``Response Object Examples: Response of an array of a complex type`` () =
    """{
      "description": "A complex object array response",
      "schema": {
        "type": "array",
        "items": {
          "$ref": "#/definitions/VeryComplexType"
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseResponseObject (Parser.ParserContext.Empty)
    |> should equal
        {
            Description = "A complex object array response"
            Schema = Some <| Array (Reference "#/definitions/VeryComplexType")
        }



[<Test>]
let ``Response Object Examples: Response with a string type`` () =
    """{
      "description": "A simple string response",
      "schema": {
        "type": "string"
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseResponseObject (Parser.ParserContext.Empty)
    |> should equal
        {
            Description = "A simple string response"
            Schema = Some String
        }


[<Test>]
let ``Response Object Examples: Response with headers`` () =
    """{
      "description": "A simple string response",
      "schema": {
        "type": "string"
      },
      "headers": {
        "X-Rate-Limit-Limit": {
          "description": "The number of allowed requests in the current period",
          "type": "integer"
        },
        "X-Rate-Limit-Remaining": {
          "description": "The number of remaining requests in the current period",
          "type": "integer"
        },
        "X-Rate-Limit-Reset": {
          "description": "The number of seconds left in the current period",
          "type": "integer"
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseResponseObject (Parser.ParserContext.Empty)
    |> should equal
        {
            Description = "A simple string response"
            Schema = Some String
        }


[<Test>]
let ``Response Object Examples: Response with no return value`` () =
    """{
      "description": "object created"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseResponseObject (Parser.ParserContext.Empty)
    |> should equal
        {
            Description = "object created"
            Schema = None
        }



[<Test>]
let ``Tag Object Example`` () =
    """{
        "name": "pet",
        "description": "Pets operations"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseTagObject
    |> should equal
        ({
            Name = "pet"
            Description = "Pets operations"
        }:TagObject)


[<Test>]
let ``Reference Object Example`` () =
    """{
        "$ref": "#/definitions/Pet"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Reference "#/definitions/Pet")


[<Test>]
let ``Schema Object Examples: Primitive Sample`` () =
    """{
        "type": "string",
        "format": "email"
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        String


[<Test>]
let ``Schema Object Examples: Simple Model`` () =
    """{
      "type": "object",
      "required": [
        "name"
      ],
      "properties": {
        "name": {
          "type": "string"
        },
        "address": {
          "$ref": "#/definitions/Address"
        },
        "age": {
          "type": "integer",
          "format": "int32",
          "minimum": 0
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Object
            [|{Name = "name"
               Type = String
               IsRequired = true
               Description = ""}
              {Name = "address"
               Type = Reference "#/definitions/Address"
               IsRequired = false
               Description = ""}
              {Name = "age"
               Type = Int32
               IsRequired = false
               Description = ""}|]
        )


[<Test>]
let ``Schema Object Examples: Model with Map/Dictionary Properties: For a simple string to string mapping`` () =
    """{
      "type": "object",
      "additionalProperties": {
        "type": "string"
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Dictionary String)


[<Test>]
let ``Schema Object Examples: Model with Map/Dictionary Properties: For a string to model mapping`` () =
    """{
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/ComplexModel"
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Dictionary (Reference "#/definitions/ComplexModel"))


[<Test>]
let ``Schema Object Examples: Model with Example`` () =
    """{
      "type": "object",
      "properties": {
        "id": {
          "type": "integer",
          "format": "int64"
        },
        "name": {
          "type": "string"
        }
      },
      "required": [
        "name"
      ],
      "example": {
        "name": "Puma",
        "id": 1
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Object
            [|{Name = "id"
               Type = Int64
               IsRequired = false
               Description = ""}
              {Name = "name"
               Type = String
               IsRequired = true
               Description = ""}|]
        )


[<Test>]
let ``Schema Object Examples: Models with Composition`` () =
    """{
        "ErrorModel": {
          "type": "object",
          "required": [
            "message",
            "code"
          ],
          "properties": {
            "message": {
              "type": "string"
            },
            "code": {
              "type": "integer",
              "minimum": 100,
              "maximum": 600
            }
          }
        },
        "ExtendedErrorModel": {
          "allOf": [
            {
              "$ref": "#/definitions/ErrorModel"
            },
            {
              "type": "object",
              "required": [
                "rootCause"
              ],
              "properties": {
                "rootCause": {
                  "type": "string"
                }
              }
            }
          ]
        }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseDefinitionsObject
    |> should equal
        [|
            "#/definitions/ErrorModel",
            (Object
                [|{ Name = "message"
                    Type = String
                    IsRequired = true
                    Description = "" }
                  { Name = "code"
                    Type = Int64
                    IsRequired = true
                    Description = "" }|])
            "#/definitions/ExtendedErrorModel",
            (Object
                [|{ Name = "message"
                    Type = String
                    IsRequired = true
                    Description = "" }
                  { Name = "code"
                    Type = Int64
                    IsRequired = true
                    Description = "" }
                  { Name = "rootCause"
                    Type = String
                    IsRequired = true
                    Description = "" }|])
        |]


[<Test; Ignore("Not supported")>]
let ``Schema Object Examples: Models with Polymorphism Support`` () =
    """{
      "definitions": {
        "Pet": {
          "type": "object",
          "discriminator": "petType",
          "properties": {
            "name": {
              "type": "string"
            },
            "petType": {
              "type": "string"
            }
          },
          "required": [
            "name",
            "petType"
          ]
        },
        "Cat": {
          "description": "A representation of a cat",
          "allOf": [
            {
              "$ref": "#/definitions/Pet"
            },
            {
              "type": "object",
              "properties": {
                "huntingSkill": {
                  "type": "string",
                  "description": "The measured skill for hunting",
                  "default": "lazy",
                  "enum": [
                    "clueless",
                    "lazy",
                    "adventurous",
                    "aggressive"
                  ]
                }
              },
              "required": [
                "huntingSkill"
              ]
            }
          ]
        },
        "Dog": {
          "description": "A representation of a dog",
          "allOf": [
            {
              "$ref": "#/definitions/Pet"
            },
            {
              "type": "object",
              "properties": {
                "packSize": {
                  "type": "integer",
                  "format": "int32",
                  "description": "the size of the pack the dog is from",
                  "default": 0,
                  "minimum": 0
                }
              },
              "required": [
                "packSize"
              ]
            }
          ]
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseSchemaObject Map.empty
    |> should equal
        (Object
            [||]
        )


[<Test>]
let ``Definitions Object Example`` () =
    """{
      "Category": {
        "type": "object",
        "properties": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "name": {
            "type": "string"
          }
        }
      },
      "Tag": {
        "type": "object",
        "properties": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "name": {
            "type": "string"
          }
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseDefinitionsObject
    |> should equal
        [|
            "#/definitions/Category",
            (Object
                [|{Name = "id"
                   Type = Int64
                   IsRequired = false
                   Description = ""}
                  {Name = "name"
                   Type = String
                   IsRequired = false
                   Description = ""}|]
            )
            "#/definitions/Tag",
            (Object
                [|{Name = "id"
                   Type = Int64
                   IsRequired = false
                   Description = ""}
                  {Name = "name"
                   Type = String
                   IsRequired = false
                   Description = ""}|]
            )
        |]


[<Test>]
let ``Parameters Definition Object Example`` () =
    """{
      "skipParam": {
        "name": "skip",
        "in": "query",
        "description": "number of items to skip",
        "required": true,
        "type": "integer",
        "format": "int32"
      },
      "limitParam": {
        "name": "limit",
        "in": "query",
        "description": "max records to return",
        "required": true,
        "type": "integer",
        "format": "int32"
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseParametersDefinition
    |> should equal
        ([|
            "#/parameters/skipParam",
            {
                Name = "skip"
                In = Query
                Description = "number of items to skip"
                Required = true
                Type = Int32
                CollectionFormat = Csv
            }
            "#/parameters/limitParam",
            {
                Name = "limit"
                In = Query
                Description = "max records to return"
                Required = true
                Type = Int32
                CollectionFormat = Csv
            }
        |] |> Map.ofArray)

[<Test>]
let ``Responses Definitions Object Example`` () =
    """{
      "NotFound": {
        "description": "Entity not found."
      },
      "IllegalInput": {
        "description": "Illegal input for operation."
      },
      "GeneralError": {
        "description": "General Error",
        "schema": {
            "$ref": "#/definitions/GeneralError"
        }
      }
    }"""
    |> JsonValue.Parse
    |> JsonNodeAdapter
    |> Parser.parseResponsesDefinition
    |> should equal
        ([|
            "#/responses/NotFound",
            {
                Description = "Entity not found."
                Schema = None
            }
            "#/responses/IllegalInput",
            {
                Description = "Illegal input for operation."
                Schema = None
            }
            "#/responses/GeneralError",
            {
                Description = "General Error"
                Schema = Some (Reference "#/definitions/GeneralError")
            }
        |] |> Map.ofArray)
