module Schema.Spec.Tests

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
    |> JsonParser.parseInfoObject
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
    |> JsonParser.parsePathsObject JsonParser.ParserContext.Empty
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
    |> JsonParser.parsePathsObject JsonParser.ParserContext.Empty
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
    |> JsonParser.parseOperationObject JsonParser.ParserContext.Empty "/" Get
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
    |> JsonParser.parseParameterObject
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
    |> JsonParser.parseParameterObject
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
    |> JsonParser.parseParameterObject
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
    |> JsonParser.parseParameterObject
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
    |> JsonParser.parseParameterObject
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
    |> JsonParser.parseParameterObject
    |> should equal
        {
            Name = "avatar"
            In = FormData
            Description = "The avatar of the user"
            Required = true
            Type = File
            CollectionFormat = Csv
        }


// !!! Items Object Examples