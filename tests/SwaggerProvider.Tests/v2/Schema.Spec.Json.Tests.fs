module SwaggerProvider.Tests.v2.Schema_Spec_Json_Tests

open SwaggerProvider.Internal.v2.Parser.Schema
open SwaggerProvider.Internal.v2.Parser
open FsUnitTyped
open Xunit

[<Fact>]
let ``Info Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseInfoObject
    |> shouldEqual
        {
            Title = "Swagger Sample App"
            Description = "This is a sample server Petstore server."
            Version = "1.0.1"
        }

[<Fact>]
let ``Paths Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parsePathsObject Parsers.ParserContext.Empty
    |> shouldEqual [|
        {
            Path = "/pets"
            Type = Get
            Tags = [||]
            Summary = ""
            Description = "Returns all pets from the system that the user has access to"
            OperationId = ""
            Consumes = [||]
            Produces = [| "application/json" |]
            Responses =
                [|
                    Some(200),
                    {
                        Description = "A list of pets."
                        Schema = Some <| Array(Reference "#/definitions/pet")
                    }
                |]
            Parameters = [||]
            Deprecated = false
        }
    |]

[<Fact>]
let ``Path Item Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parsePathsObject Parsers.ParserContext.Empty
    |> shouldEqual [|
        {
            Path = "/pets"
            Type = Get
            Tags = [||]
            Summary = "Find pets by ID"
            Description = "Returns pets based on ID"
            OperationId = "getPetsById"
            Consumes = [||]
            Produces = [| "application/json"; "text/html" |]
            Responses =
                [|
                    Some(200),
                    {
                        Description = "pet response"
                        Schema = Some <| Array(Reference "#/definitions/Pet")
                    }
                    None,
                    {
                        Description = "error payload"
                        Schema = Some <| Reference "#/definitions/ErrorModel"
                    }
                |]
            Parameters =
                [|
                    {
                        Name = "id"
                        In = Path
                        Description = "ID of pet to use"
                        Required = true
                        Type = Array String
                        CollectionFormat = Csv
                    }
                |]
            Deprecated = false
        }
    |]

[<Fact>]
let ``Operation Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseOperationObject Parsers.ParserContext.Empty "/" Get
    |> shouldEqual
        {
            Path = "/"
            Type = Get
            Tags = [| "pet" |]
            Summary = "Updates a pet in the store with form data"
            Description = ""
            OperationId = "updatePetWithForm"
            Consumes = [| "application/x-www-form-urlencoded" |]
            Produces = [| "application/json"; "application/xml" |]
            Responses =
                [|
                    Some(200),
                    {
                        Description = "Pet updated."
                        Schema = None
                    }
                    Some(405),
                    {
                        Description = "Invalid input"
                        Schema = None
                    }
                |]
            Parameters =
                [|
                    {
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
                    }
                |]
            Deprecated = false
        }

[<Fact>]
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
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "user"
            In = Body
            Description = "user to add to the system"
            Required = true
            Type = Reference "#/definitions/User"
            CollectionFormat = Csv
        }

[<Fact>]
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
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "user"
            In = Body
            Description = "user to add to the system"
            Required = true
            Type = Array String
            CollectionFormat = Csv
        }

[<Fact>]
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
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "token"
            In = Header
            Description = "token to be passed as a header"
            Required = true
            Type = Array Int64
            CollectionFormat = Csv
        }

[<Fact>]
let ``Parameter Object Examples: Other Parameters - Path String``() =
    """{
      "name": "username",
      "in": "path",
      "description": "username to fetch",
      "required": true,
      "type": "string"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "username"
            In = Path
            Description = "username to fetch"
            Required = true
            Type = String
            CollectionFormat = Csv
        }

[<Fact>]
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
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "id"
            In = Query
            Description = "ID of the object to fetch"
            Required = false
            Type = Array String
            CollectionFormat = Multi
        }

[<Fact>]
let ``Parameter Object Examples: Other Parameters - File``() =
    """{
      "name": "avatar",
      "in": "formData",
      "description": "The avatar of the user",
      "required": true,
      "type": "file"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "avatar"
            In = FormData
            Description = "The avatar of the user"
            Required = true
            Type = File
            CollectionFormat = Csv
        }

[<Fact>]
let ``Response Object Examples: Response of an array of a complex type``() =
    """{
      "description": "A complex object array response",
      "schema": {
        "type": "array",
        "items": {
          "$ref": "#/definitions/VeryComplexType"
        }
      }
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseResponseObject(Parsers.ParserContext.Empty)
    |> shouldEqual
        {
            Description = "A complex object array response"
            Schema = Some <| Array(Reference "#/definitions/VeryComplexType")
        }

[<Fact>]
let ``Response Object Examples: Response with a string type``() =
    """{
      "description": "A simple string response",
      "schema": {
        "type": "string"
      }
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseResponseObject(Parsers.ParserContext.Empty)
    |> shouldEqual
        {
            Description = "A simple string response"
            Schema = Some String
        }

[<Fact>]
let ``Response Object Examples: Response with headers``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseResponseObject(Parsers.ParserContext.Empty)
    |> shouldEqual
        {
            Description = "A simple string response"
            Schema = Some String
        }

[<Fact>]
let ``Response Object Examples: Response with no return value``() =
    """{
        "description": "object created"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseResponseObject(Parsers.ParserContext.Empty)
    |> shouldEqual
        {
            Description = "object created"
            Schema = None
        }

[<Fact>]
let ``Tag Object Example``() =
    """{
        "name": "pet",
        "description": "Pets operations"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseTagObject
    |> shouldEqual(
        {
            Name = "pet"
            Description = "Pets operations"
        }: TagObject
    )


[<Fact>]
let ``Tag Object Example Ref``() =
    """{
        "$ref": "#/definitions/Pet"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(Reference "#/definitions/Pet")

[<Fact>]
let ``Schema Object Examples: Primitive Sample``() =
    """{
        "type": "string",
        "format": "email"
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual String

[<Fact>]
let ``Schema Object Examples: Simple Model``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(
        Object [|
            {
                Name = "name"
                Type = String
                IsRequired = true
                Description = ""
            }
            {
                Name = "address"
                Type = Reference "#/definitions/Address"
                IsRequired = false
                Description = ""
            }
            {
                Name = "age"
                Type = Int32
                IsRequired = false
                Description = ""
            }
        |]
    )

[<Fact>]
let ``Schema Object Examples: Model with Map/Dictionary Properties: For a simple string to string mapping``() =
    """{
      "type": "object",
      "additionalProperties": {
        "type": "string"
      }
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(Dictionary String)

[<Fact>]
let ``Schema Object Examples: Model with Map/Dictionary Properties: For a string to model mapping``() =
    """{
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/ComplexModel"
      }
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(Dictionary(Reference "#/definitions/ComplexModel"))

[<Fact>]
let ``Schema Object Examples: Model with Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(
        [|
            {
                Name = "id"
                Type = Int64
                IsRequired = false
                Description = ""
            }
            {
                Name = "name"
                Type = String
                IsRequired = true
                Description = ""
            }
        |]
        |> Object
    )

[<Fact>]
let ``Schema Object Examples: Models with Composition``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseDefinitionsObject
    |> Seq.map(fun x -> x.Key, x.Value.Value)
    |> Map.ofSeq
    |> shouldEqual(
        [|
            "#/definitions/ErrorModel",
            (Object [|
                {
                    Name = "message"
                    Type = String
                    IsRequired = true
                    Description = ""
                }
                {
                    Name = "code"
                    Type = Int64
                    IsRequired = true
                    Description = ""
                }
            |])
            "#/definitions/ExtendedErrorModel",
            (Object [|
                {
                    Name = "message"
                    Type = String
                    IsRequired = true
                    Description = ""
                }
                {
                    Name = "code"
                    Type = Int64
                    IsRequired = true
                    Description = ""
                }
                {
                    Name = "rootCause"
                    Type = String
                    IsRequired = true
                    Description = ""
                }
            |])
        |]
        |> Map.ofArray
    )

[<Fact>]
let ``Schema Object Examples: Models with Polymorphism Support``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseSchemaObject Parsers.emptyDict
    |> shouldEqual(Object [||])

[<Fact>]
let ``Definitions Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseDefinitionsObject
    |> Seq.map(fun x -> x.Key, x.Value.Value)
    |> Map.ofSeq
    |> shouldEqual(
        [|
            "#/definitions/Category",
            (Object [|
                {
                    Name = "id"
                    Type = Int64
                    IsRequired = false
                    Description = ""
                }
                {
                    Name = "name"
                    Type = String
                    IsRequired = false
                    Description = ""
                }
            |])
            "#/definitions/Tag",
            (Object [|
                {
                    Name = "id"
                    Type = Int64
                    IsRequired = false
                    Description = ""
                }
                {
                    Name = "name"
                    Type = String
                    IsRequired = false
                    Description = ""
                }
            |])
        |]
        |> Map.ofArray
    )

[<Fact>]
let ``Parameters Definition Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseParametersDefinition Parsers.emptyDict
    |> shouldEqual(
        [|
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
        |]
        |> Map.ofArray
    )

[<Fact>]
let ``Responses Definitions Object Example``() =
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
    |> SwaggerParser.parseJson
    |> Parsers.parseResponsesDefinition
    |> shouldEqual(
        [|
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
                Schema = Some(Reference "#/definitions/GeneralError")
            }
        |]
        |> Map.ofArray
    )

[<Fact>]
let ``Parameter Map Examples: Body Parameters Map``() =
    """{
      "name": "user",
      "in": "body",
      "description": "user to add to the system",
      "required": true,
      "schema": {
        "type": "object",
        "additionalProperties": {
          "type": "string"
        }
      }
    }"""
    |> SwaggerParser.parseJson
    |> Parsers.parseParameterObject Parsers.emptyDict
    |> shouldEqual
        {
            Name = "user"
            In = Body
            Description = "user to add to the system"
            Required = true
            Type = Dictionary String
            CollectionFormat = Csv
        }
