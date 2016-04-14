module Swagger.PetStore.Tests

open SwaggerProvider
open FSharp.Data
open NUnit.Framework
open FsUnitTyped

type PetStore = SwaggerProvider<"http://petstore.swagger.io/v2/swagger.json", "Content-Type=application/json">
let store = PetStore()

let apiKey = "special-key"

[<Test>]
let ``Test provided Host property`` () =
    store.Host |> shouldEqual "petstore.swagger.io"
    store.Host <- "test"
    store.Host |> shouldEqual "test"
    store.Host <- "petstore.swagger.io"
    store.Host |> shouldEqual "petstore.swagger.io"

[<Test>]
let ``instantiate provided objects`` () =
    let pet = PetStore.Pet(Name = "foo")
    pet.Name |> shouldEqual "foo"
    pet.Name <- "bar"
    pet.Name |> shouldEqual "bar"

[<Test>]
let ``call provided methods`` () =
    try
        store.DeletePet(1337L, apiKey)
    with
    | exn -> ()

    let tag = PetStore.Tag (Name = "foobar")
    let pet = PetStore.Pet (Name = "foo", Id = Some 1337L, Status = "available")

    try
        store.AddPet(pet)
    with
    | exn -> Assert.Fail ("Adding pet failed with message: " + exn.Message)

    let pet2 = store.GetPetById(1337L)
    pet.Name        |> shouldEqual pet2.Name
    pet.Id          |> shouldEqual pet.Id
    pet.Category    |> shouldEqual pet2.Category
    pet.Status      |> shouldEqual pet2.Status
    pet             |> shouldNotEqual pet2

