module SwaggerProvider.Tests.Schema_OperationCompilationTests

/// Unit tests for the v3 OperationCompiler — verifying generated method signatures,
/// parameter ordering, CancellationToken injection, return-type resolution,
/// and async vs task mode.

open System
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Xunit
open FsUnitTyped

// ── Helpers ───────────────────────────────────────────────────────────────────

let private compileTaskSchema schemaStr =
    compileV3Schema schemaStr false

let private compileAsyncSchema schemaStr =
    compileV3Schema schemaStr true

let private findMethod (types: ProviderImplementation.ProvidedTypes.ProvidedTypeDefinition list) (methodName: string) =
    types
    |> List.collect(fun t -> t.GetMethods() |> Array.toList)
    |> List.tryFind(fun m -> m.Name = methodName)

// ── Simple GET with no parameters ─────────────────────────────────────────────

let private simpleGetSchema =
    """openapi: "3.0.0"
info:
  title: SimpleGetTest
  version: "1.0.0"
paths:
  /status:
    get:
      operationId: getStatus
      summary: Get server status
      responses:
        "200":
          description: OK
          content:
            application/json:
              schema:
                type: string
components:
  schemas: {}
"""

[<Fact>]
let ``GET endpoint generates a method with the operation name``() =
    let types = compileTaskSchema simpleGetSchema
    let method = findMethod types "GetStatus"
    method.IsSome |> shouldEqual true

[<Fact>]
let ``GET endpoint with no parameters has CancellationToken as its only parameter``() =
    let types = compileTaskSchema simpleGetSchema
    let method = (findMethod types "GetStatus").Value
    let parameters = method.GetParameters()
    parameters.Length |> shouldEqual 1
    parameters[0].ParameterType |> shouldEqual typeof<CancellationToken>

[<Fact>]
let ``GET endpoint returning JSON string has Task<string> return type``() =
    let types = compileTaskSchema simpleGetSchema
    let method = (findMethod types "GetStatus").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Task<_>>

    method.ReturnType.GetGenericArguments()[0]
    |> shouldEqual typeof<string>

// ── GET with required and optional path/query parameters ──────────────────────

let private parametrisedGetSchema =
    """openapi: "3.0.0"
info:
  title: ParameterisedGetTest
  version: "1.0.0"
paths:
  /items/{id}:
    get:
      operationId: getItem
      summary: Get item by ID
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
            format: int64
        - name: tag
          in: query
          required: false
          schema:
            type: string
      responses:
        "200":
          description: OK
components:
  schemas: {}
"""

[<Fact>]
let ``GET with required + optional params orders required before optional``() =
    let types = compileTaskSchema parametrisedGetSchema
    let method = (findMethod types "GetItem").Value
    let parameters = method.GetParameters()
    // Expected: id (required int64), tag (optional string), cancellationToken (CT)
    parameters.Length |> shouldEqual 3

    let idParam = parameters[0]
    let tagParam = parameters[1]
    let ctParam = parameters[2]

    idParam.Name |> shouldEqual "id"
    idParam.ParameterType |> shouldEqual typeof<int64>
    // optional — marked as optional via ParameterAttributes
    tagParam.Name |> shouldEqual "tag"
    tagParam.IsOptional |> shouldEqual true
    ctParam.ParameterType |> shouldEqual typeof<CancellationToken>

[<Fact>]
let ``CancellationToken is always the last parameter``() =
    let types = compileTaskSchema parametrisedGetSchema
    let method = (findMethod types "GetItem").Value
    let parameters = method.GetParameters()
    let last = parameters |> Array.last
    last.ParameterType |> shouldEqual typeof<CancellationToken>

// ── POST with JSON request body ───────────────────────────────────────────────

let private postWithBodySchema =
    """openapi: "3.0.0"
info:
  title: PostBodyTest
  version: "1.0.0"
paths:
  /items:
    post:
      operationId: createItem
      summary: Create a new item
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/NewItem'
      responses:
        "201":
          description: Created
components:
  schemas:
    NewItem:
      type: object
      required:
        - name
      properties:
        name:
          type: string
"""

[<Fact>]
let ``POST with body generates method with body parameter before CancellationToken``() =
    let types = compileTaskSchema postWithBodySchema
    let method = (findMethod types "CreateItem").Value
    let parameters = method.GetParameters()
    // Expected: body (required NewItem), cancellationToken (CT)
    parameters.Length |> shouldEqual 2
    let bodyParam = parameters[0]
    let ctParam = parameters[1]
    // Body parameter is a provided type, so just verify it is not CancellationToken
    bodyParam.ParameterType |> shouldNotEqual typeof<CancellationToken>
    ctParam.ParameterType |> shouldEqual typeof<CancellationToken>

[<Fact>]
let ``POST with no response body has Task<unit> return type``() =
    let types = compileTaskSchema postWithBodySchema
    let method = (findMethod types "CreateItem").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Task<_>>

    method.ReturnType.GetGenericArguments()[0] |> shouldEqual typeof<unit>

// ── CancellationToken naming collision avoidance ──────────────────────────────

let private ctCollisionSchema =
    """openapi: "3.0.0"
info:
  title: CTCollisionTest
  version: "1.0.0"
paths:
  /search:
    get:
      operationId: search
      parameters:
        - name: cancellationToken
          in: query
          required: false
          schema:
            type: string
      responses:
        "200":
          description: OK
components:
  schemas: {}
"""

[<Fact>]
let ``when a query param is named cancellationToken the injected CT param gets a unique name``() =
    let types = compileTaskSchema ctCollisionSchema
    let method = (findMethod types "Search").Value
    let parameters = method.GetParameters()
    // There should be two parameters: the query param + the CT param
    parameters.Length |> shouldEqual 2
    let ctParam = parameters |> Array.last
    // The injected CT param must not collide with the API param name
    ctParam.ParameterType |> shouldEqual typeof<CancellationToken>
    ctParam.Name |> shouldNotEqual parameters[0].Name

// ── Multiple operations — each gets its own CT parameter ─────────────────────

let private multiOpSchema =
    """openapi: "3.0.0"
info:
  title: MultiOpTest
  version: "1.0.0"
paths:
  /pets:
    get:
      operationId: listPets
      responses:
        "200":
          description: OK
    post:
      operationId: createPet
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              properties:
                name:
                  type: string
      responses:
        "201":
          description: Created
components:
  schemas: {}
"""

[<Fact>]
let ``multiple operations each generate a method with CancellationToken``() =
    let types = compileTaskSchema multiOpSchema

    let listPets = findMethod types "ListPets"
    let createPet = findMethod types "CreatePet"

    listPets.IsSome |> shouldEqual true
    createPet.IsSome |> shouldEqual true

    let listPetsParams = listPets.Value.GetParameters()
    let createPetParams = createPet.Value.GetParameters()

    (listPetsParams |> Array.last).ParameterType
    |> shouldEqual typeof<CancellationToken>

    (createPetParams |> Array.last).ParameterType
    |> shouldEqual typeof<CancellationToken>

// ── Async mode: asAsync=true returns Async<T> instead of Task<T> ──────────────

[<Fact>]
let ``asAsync=true: GET returning string produces Async<string> return type``() =
    let types = compileAsyncSchema simpleGetSchema
    let method = (findMethod types "GetStatus").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Async<_>>

    method.ReturnType.GetGenericArguments()[0]
    |> shouldEqual typeof<string>

[<Fact>]
let ``asAsync=true: POST with no response body produces Async<unit> return type``() =
    let types = compileAsyncSchema postWithBodySchema
    let method = (findMethod types "CreateItem").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Async<_>>

    method.ReturnType.GetGenericArguments()[0] |> shouldEqual typeof<unit>

[<Fact>]
let ``asAsync=true: method is still generated with correct name``() =
    let types = compileAsyncSchema simpleGetSchema
    let method = findMethod types "GetStatus"
    method.IsSome |> shouldEqual true

[<Fact>]
let ``asAsync=true: CancellationToken is still the last parameter``() =
    let types = compileAsyncSchema parametrisedGetSchema
    let method = (findMethod types "GetItem").Value
    let parameters = method.GetParameters()

    (parameters |> Array.last).ParameterType
    |> shouldEqual typeof<CancellationToken>

// ── DELETE / PUT / PATCH operations ──────────────────────────────────────────

let private deleteEndpointSchema =
    """openapi: "3.0.0"
info:
  title: DeleteTest
  version: "1.0.0"
paths:
  /items/{id}:
    delete:
      operationId: deleteItem
      summary: Delete an item
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      responses:
        "204":
          description: No Content
components:
  schemas: {}
"""

[<Fact>]
let ``DELETE endpoint generates a method``() =
    let types = compileTaskSchema deleteEndpointSchema
    let method = findMethod types "DeleteItem"
    method.IsSome |> shouldEqual true

[<Fact>]
let ``DELETE endpoint with 204 response produces Task<unit> return type``() =
    let types = compileTaskSchema deleteEndpointSchema
    let method = (findMethod types "DeleteItem").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Task<_>>

    method.ReturnType.GetGenericArguments()[0] |> shouldEqual typeof<unit>

[<Fact>]
let ``DELETE endpoint path parameter is included before CancellationToken``() =
    let types = compileTaskSchema deleteEndpointSchema
    let method = (findMethod types "DeleteItem").Value
    let parameters = method.GetParameters()
    // id (required int32) + cancellationToken
    parameters.Length |> shouldEqual 2
    parameters[0].Name |> shouldEqual "id"
    parameters[0].ParameterType |> shouldEqual typeof<int32>

    (parameters |> Array.last).ParameterType
    |> shouldEqual typeof<CancellationToken>

let private putEndpointSchema =
    """openapi: "3.0.0"
info:
  title: PutTest
  version: "1.0.0"
paths:
  /items/{id}:
    put:
      operationId: updateItem
      summary: Update an item
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
            format: int64
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateItem'
      responses:
        "200":
          description: Updated
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/UpdateItem'
components:
  schemas:
    UpdateItem:
      type: object
      required:
        - name
      properties:
        name:
          type: string
"""

[<Fact>]
let ``PUT endpoint generates a method``() =
    let types = compileTaskSchema putEndpointSchema
    let method = findMethod types "UpdateItem"
    method.IsSome |> shouldEqual true

[<Fact>]
let ``PUT endpoint has path param and body param before CancellationToken``() =
    let types = compileTaskSchema putEndpointSchema
    let method = (findMethod types "UpdateItem").Value
    let parameters = method.GetParameters()
    // id (int64) + body (UpdateItem) + cancellationToken — 3 params total
    parameters.Length |> shouldEqual 3
    parameters[0].Name |> shouldEqual "id"
    parameters[0].ParameterType |> shouldEqual typeof<int64>
    // body param is a provided type (not CancellationToken)
    parameters[1].ParameterType
    |> shouldNotEqual typeof<CancellationToken>

    (parameters |> Array.last).ParameterType
    |> shouldEqual typeof<CancellationToken>

[<Fact>]
let ``PUT endpoint with JSON response produces Task<T> return type``() =
    let types = compileTaskSchema putEndpointSchema
    let method = (findMethod types "UpdateItem").Value

    method.ReturnType.IsGenericType |> shouldEqual true

    method.ReturnType.GetGenericTypeDefinition()
    |> shouldEqual typedefof<Task<_>>

    // Return type must not be unit — should be the UpdateItem provided type
    method.ReturnType.GetGenericArguments()[0]
    |> shouldNotEqual typeof<unit>

// ── Header parameters ─────────────────────────────────────────────────────────

let private headerParamSchema =
    """openapi: "3.0.0"
info:
  title: HeaderParamTest
  version: "1.0.0"
paths:
  /items:
    get:
      operationId: listItems
      parameters:
        - name: X-Api-Version
          in: header
          required: true
          schema:
            type: string
        - name: limit
          in: query
          required: false
          schema:
            type: integer
      responses:
        "200":
          description: OK
components:
  schemas: {}
"""

[<Fact>]
let ``header parameter is included as a method parameter``() =
    let types = compileTaskSchema headerParamSchema
    let method = (findMethod types "ListItems").Value
    let parameters = method.GetParameters()
    // xApiVersion (required string) + limit (optional int) + cancellationToken
    parameters.Length |> shouldEqual 3
    // Header param names are camelCased
    let paramNames = parameters |> Array.map(fun p -> p.Name)
    paramNames |> shouldContain "xApiVersion"

[<Fact>]
let ``required header parameter is not optional``() =
    let types = compileTaskSchema headerParamSchema
    let method = (findMethod types "ListItems").Value
    let parameters = method.GetParameters()
    let headerParam = parameters |> Array.find(fun p -> p.Name = "xApiVersion")
    headerParam.IsOptional |> shouldEqual false
    headerParam.ParameterType |> shouldEqual typeof<string>
