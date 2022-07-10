module Swagger.v3.PetStore.Tests

open SwaggerProvider
open Swagger
open Expecto
open System


[<Literal>]
let Schema = "https://petstore.swagger.io/v2/swagger.json"

type PetStore = OpenApiClientProvider<Schema, PreferAsync=true>

type PetStoreNullable = OpenApiClientProvider<Schema, PreferNullable=true>
type PetStoreOperationId = OpenApiClientProvider<Schema, IgnoreOperationId=true>
type PetStoreControllerPrefix = OpenApiClientProvider<Schema, IgnoreControllerPrefix=false>

let store = PetStore.Client()
let apiKey = "test-key"

[<Tests>]
let petStoreTests =
    testList "All/v3/TP PetStore Tests" [

        testCase "Test provided Host property"
        <| fun _ ->
            let store = PetStore.Client()
            Expect.equal (store.HttpClient.BaseAddress.ToString()) "https://petstore.swagger.io/v2/" "value from schema"
            store.HttpClient.BaseAddress <- Uri "http://petstore.swagger.io/v3/"
            Expect.equal (store.HttpClient.BaseAddress.ToString()) "http://petstore.swagger.io/v3/" "Modified value"

        testCase "instantiate provided objects"
        <| fun _ ->
            let pet = PetStore.Pet(Name = "foo")
            Expect.equal pet.Name "foo" "access initial value"
            Expect.stringContains (pet.ToString()) "foo" "ToString"
            pet.Name <- "bar"
            Expect.equal pet.Name "bar" "access modified value"
            Expect.stringContains (pet.ToString()) "bar" "ToString"

        testCaseAsync "throw custom exceptions"
        <| async {
            try
                let! __ = store.GetPetById(-100L)
                failwith "Call should fail"
            with :? Swagger.OpenApiException as ex ->
                Expect.equal ex.Description "Pet not found" "invalid error message"
        }

        ptestCaseAsync "call provided methods"
        <| async {
            let id = 3347L

            try
                do! store.DeletePet(id, apiKey)
            with _ ->
                ()

            let tag = PetStore.Tag(None, "foobar")
            Expect.stringContains (tag.ToString()) "foobar" "ToString"
            let pet = PetStore.Pet("foo", [||], Some id)
            Expect.stringContains (pet.ToString()) (id.ToString()) "ToString"

            try
                do! store.AddPet(pet)
            with exn ->
                let msg =
                    if isNull exn.InnerException then
                        exn.Message
                    else
                        exn.InnerException.Message

                failwithf "Adding pet failed with message: %s" msg

            let! pet2 = store.GetPetById(id)
            Expect.equal pet.Name pet2.Name "same Name"
            Expect.equal pet.Id pet2.Id "same Id"
            Expect.equal pet.Category pet2.Category "same Category"
            Expect.equal pet.Status pet2.Status "same Status"
            Expect.notEqual pet pet2 "different objects"
        }
    ]
