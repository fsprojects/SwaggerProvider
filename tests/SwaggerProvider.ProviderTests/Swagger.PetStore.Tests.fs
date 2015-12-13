module Swagger.PetStore.Tests

open SwaggerProvider
open FSharp.Data
open NUnit.Framework
open FsUnit

type PetStore = SwaggerProvider<"http://petstore.swagger.io/v2/swagger.json", "Content-Type=application/json">

let apiKey = "special-key"

[<Test>]
let ``instantiate provided objects`` () =
    let pet = PetStore.Definitions.Pet(Name = "foo")
    pet.Name |> should equal "foo"
    pet.Name <- "bar"
    pet.Name |> should equal "bar"

[<Test>]
let ``call provided methods`` () =
    try
        PetStore.Pet.DeletePet(1337L, apiKey)
    with
    | exn -> ()

    let tag = PetStore.Definitions.Tag (Name = "foobar")
    let pet = PetStore.Definitions.Pet (Name = "foo", Id = Some 1337L, Status = "available")

    try
        PetStore.Pet.AddPet(pet)
    with
    | exn -> Assert.Fail ("Adding pet failed with message: " + exn.Message)

    let pet2 = PetStore.Pet.GetPetById(1337L)
    pet.Name        |> should equal pet2.Name
    pet.Id          |> should equal pet.Id
    pet.Category    |> should equal pet2.Category
    pet.Status      |> should equal pet2.Status
    pet             |> should not' (equal pet2)

