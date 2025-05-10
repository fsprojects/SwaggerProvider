module Swagger.v3.PetStore.Tests

open SwaggerProvider
open Swagger
open FsUnitTyped
open Xunit
open System

[<Literal>]
let Schema = "https://petstore.swagger.io/v2/swagger.json"

type PetStore = OpenApiClientProvider<Schema, PreferAsync=true>
type PetStoreTask = OpenApiClientProvider<Schema, PreferAsync=false>

type PetStoreNullable = OpenApiClientProvider<Schema, PreferNullable=true>
type PetStoreOperationId = OpenApiClientProvider<Schema, IgnoreOperationId=true>
type PetStoreControllerPrefix = OpenApiClientProvider<Schema, IgnoreControllerPrefix=false>

let store = PetStore.Client()
let storeTask = PetStoreTask.Client()
let apiKey = "test-key"

[<Fact>]
let ``Test provided Host property``() =
    let store = PetStore.Client()

    store.HttpClient.BaseAddress.ToString()
    |> shouldEqual "https://petstore.swagger.io/v2/"

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
            let! _ = store.GetPetById(-142L)
            failwith "Call should fail"
        with :? System.AggregateException as aex ->
            match aex.InnerException with
            | :? OpenApiException as ex -> ex.Description |> shouldEqual "Pet not found"
            | _ -> raise aex
    }

[<Fact>]
let ``throw custom exceptions from task``() =
    task {
        try
            let! _ = storeTask.GetPetById(-142L)
            failwith "Call should fail"
        with :? OpenApiException as ex ->
            ex.Description |> shouldEqual "Pet not found"
    }

[<Fact>]
let ``call provided methods``() =
    task {
        let id = 3247L

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
