module Swagger.PetStore.Tests

open SwaggerProvider
open Swagger
open Xunit
open FsUnitTyped
open System

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/petstore.json"

type PetStore = SwaggerClientProvider<Schema, PreferAsync=true>
type PetStoreTask = SwaggerClientProvider<Schema, PreferAsync=false>
type PetStoreNullable = SwaggerClientProvider<Schema, PreferNullable=true>

type PetStoreOperationId = SwaggerClientProvider<Schema, IgnoreOperationId=true>
type PetStoreControllerPrefix = SwaggerClientProvider<Schema, IgnoreControllerPrefix=false>

let store = PetStore.Client()
let storeTask = PetStoreTask.Client()
let apiKey = "test-key"

[<Fact>]
let ``Test provided Host property``() =
    let store = PetStore.Client()

    store.HttpClient.BaseAddress.ToString()
    |> shouldEqual "https://petstore.swagger.io/"

    store.HttpClient.BaseAddress <- Uri "http://petstore.swagger.io/v3/"

    store.HttpClient.BaseAddress.ToString()
    |> shouldEqual "http://petstore.swagger.io/v3/"

[<Fact>]
let ``Instantiate provided objects``() =
    let pet = PetStore.Pet(Name = "foo")
    pet.Name |> shouldEqual "foo"
    pet.ToString() |> shouldContainText "foo"
    pet.Name <- "bar"
    pet.Name |> shouldEqual "bar"
    pet.ToString() |> shouldContainText "bar"

[<Fact>]
let ``throw custom exceptions from async``() =
    task {
        try
            let! _ = store.GetPetById(242L)
            failwith "Call should fail"
        with :? System.AggregateException as aex ->
            match aex.InnerException with
            | :? System.Net.Http.HttpRequestException as ex -> ex.Message |> shouldContainText "Not Found"
            | _ -> raise aex
    }

[<Fact>]
let ``throw custom exceptions from task``() =
    task {
        try
            let! _ = storeTask.GetPetById(342L)
            failwith "Call should fail"
        with :? System.Net.Http.HttpRequestException as ex ->
            ex.Message |> shouldContainText "Not Found"
    }

[<Fact>]
let ``call provided methods``() =
    task {
        let id = 3147L

        try
            do! store.DeletePet(id, apiKey)
        with _ ->
            ()

        let tag = PetStore.Tag(None, "foobar")
        tag.Name |> shouldEqual "foobar"
        let pet = PetStore.Pet("foo", [||], Some id)
        pet.ToString() |> shouldContainText(id.ToString())

        try
            do! store.AddPet(pet)
        with exn ->
            let msg =
                if isNull exn.InnerException then
                    exn.Message
                else
                    exn.InnerException.Message

            failwith $"Adding pet failed with message: %s{msg}"

        let! pet2 = store.GetPetById(id)
        pet.Name |> shouldEqual pet2.Name
        pet.Id |> shouldEqual pet2.Id
        pet.Category |> shouldEqual pet2.Category
        pet.Status |> shouldEqual pet2.Status
        pet |> shouldNotEqual pet2
    }

[<Fact>]
let ``create types with Nullable properties``() =
    let tag = PetStoreNullable.Tag(Nullable<_>(), "foobar")
    tag.Name |> shouldEqual "foobar"
    let tag2 = PetStoreNullable.Tag(Name = "foobar")
    tag2.ToString() |> shouldContainText "foobar"

    let pet = PetStoreNullable.Pet("foo", [||], Nullable(1337L))
    pet.ToString() |> shouldContainText "1337"
    let pet2 = PetStoreNullable.Pet(Name = "foo", Id = Nullable(1337L))
    pet2.ToString() |> shouldContainText "1337"
