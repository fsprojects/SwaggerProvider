module Swagger.PetStore.Tests

open SwaggerProvider
open FSharp.Data
open Expecto

type PetStore = SwaggerProvider<"http://petstore.swagger.io/v2/swagger.json", "Content-Type=application/json">
let store = PetStore()

let apiKey = "special-key"


[<Tests>]
let petStoreTests =
  testList "All/TP PetStore Tests" [

    testCase "Test provided Host property" <| fun _ ->
        Expect.equal store.Host "http://petstore.swagger.io" "value from schema"
        store.Host <- "https://petstore.swagger.io"
        Expect.equal store.Host "https://petstore.swagger.io" "Modified value"
        store.Host <- "http://petstore.swagger.io"
        Expect.equal store.Host "http://petstore.swagger.io" "original value"

    testCase "instantiate provided objects" <| fun _ ->
        let pet = PetStore.Pet(Name = "foo")
        Expect.equal pet.Name "foo" "access initial value"
        Expect.stringContains (pet.ToString()) "foo" "ToString"
        pet.Name <- "bar"
        Expect.equal pet.Name "bar" "access modified value"
        Expect.stringContains (pet.ToString()) "bar" "ToString"

    testCase "call provided methods" <| fun _ ->
        try
            store.DeletePet(1337L, apiKey)
        with
        | exn -> ()

        let tag = PetStore.Tag (Name = "foobar")
        Expect.stringContains (tag.ToString()) "foobar" "ToString"
        let pet = PetStore.Pet (Name = "foo", Id = Some 1337L, Status = "available")
        Expect.stringContains (pet.ToString()) "1337" "ToString"

        try
            store.AddPet(pet)
        with
        | exn -> failwithf "Adding pet failed with message: %s" exn.Message

        let pet2 = store.GetPetById(1337L)
        Expect.equal pet.Name     pet2.Name     "same Name"
        Expect.equal pet.Id       pet.Id        "same Id"
        Expect.equal pet.Category pet2.Category "same Category"
        Expect.equal pet.Status   pet2.Status   "same Status"
        Expect.notEqual pet pet2 "different objects"
  ]
