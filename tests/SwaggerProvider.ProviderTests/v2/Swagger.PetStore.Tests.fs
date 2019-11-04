module Swagger.PetStore.Tests

open SwaggerProvider
open Expecto
open System

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/petstore.json"
type PetStore = SwaggerClientProvider<Schema, PreferAsync = true>
type PetStoreNullable = SwaggerClientProvider<Schema, PreferNullable = true>
let store = PetStore.Client()
let apiKey = "test-key"

[<Tests>]
let petStoreTests =
  testList "All/TP PetStore Tests" [

    testCase "Test provided Host property" <| fun _ ->
        let store = PetStore.Client()
        Expect.equal (store.HttpClient.BaseAddress.ToString()) "https://petstore.swagger.io/" "value from schema"
        store.HttpClient.BaseAddress <- Uri "http://petstore.swagger.io/"
        Expect.equal (store.HttpClient.BaseAddress.ToString()) "http://petstore.swagger.io/" "Modified value"

    testCase "instantiate provided objects" <| fun _ ->
        let pet = PetStore.Pet(Name = "foo")
        Expect.equal pet.Name "foo" "access initial value"
        Expect.stringContains (pet.ToString()) "foo" "ToString"
        pet.Name <- "bar"
        Expect.equal pet.Name "bar" "access modified value"
        Expect.stringContains (pet.ToString()) "bar" "ToString"

    testCaseAsync "call provided methods" <| async {
        try
            do! store.DeletePet(1337L, apiKey)
        with
        | _ -> ()

        let tag = PetStore.Tag(None, "foobar")
        Expect.stringContains (tag.ToString()) "foobar" "ToString"
        let pet = PetStore.Pet("foo", [||], Some 1337L)
        Expect.stringContains (pet.ToString()) "1337" "ToString"

        try
            do! store.AddPet(pet)
        with
        | exn ->
            let msg = if exn.InnerException = null then exn.Message
                      else exn.InnerException.Message
            failwithf "Adding pet failed with message: %s" msg

        let! pet2 = store.GetPetById(1337L)
        Expect.equal pet.Name     pet2.Name     "same Name"
        Expect.equal pet.Id       pet2.Id        "same Id"
        Expect.equal pet.Category pet2.Category "same Category"
        Expect.equal pet.Status   pet2.Status   "same Status"
        Expect.notEqual pet pet2 "different objects"
    }

    testCase "create types with Nullable properties" <| fun _ ->
        let tag = PetStoreNullable.Tag(Nullable<_>(), "foobar")
        Expect.stringContains (tag.ToString()) "foobar" "ToString"
        let tag2 = PetStoreNullable.Tag (Name = "foobar")
        Expect.stringContains (tag2.ToString()) "foobar" "ToString"

        let pet = PetStoreNullable.Pet("foo", [||], Nullable(1337L))
        Expect.stringContains (pet.ToString()) "1337" "ToString"
        let pet2 = PetStoreNullable.Pet (Name="foo", Id = Nullable(1337L))
        Expect.stringContains (pet2.ToString()) "1337" "ToString"
  ]
