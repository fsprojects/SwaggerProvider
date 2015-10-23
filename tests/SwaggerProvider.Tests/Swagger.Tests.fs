module Swagger.Tests

open SwaggerProvider
open FSharp.Data
open NUnit.Framework

[<Literal>]
let filePath =  __SOURCE_DIRECTORY__ + "\Schemas\PetStore.Swagger.json"
type PetStore = SwaggerProvider<filePath, "Content-Type,application/json">

let apiKey = "special-key"

[<Test>]
let ``instantiate provided objects`` () =
    let pet = new PetStore.Definitions.Pet(Name = "foo")
    Assert.AreEqual (pet.Name, "foo")
    pet.Name <- "bar"
    Assert.AreEqual (pet.Name, "bar")

[<Test>]
let ``call provided methods`` () =
    try 
        PetStore.Pet.DeletePet(1337L, apiKey)
    with 
    | exn -> ()

    let tag = new PetStore.Definitions.Tag (Name = "foobar")
    let pet = new PetStore.Definitions.Pet (Name = "foo", Id = Some 1337L, Tags = [|tag|], Status = "available")
    
    try 
        PetStore.Pet.AddPet(pet)
    with
    | exn -> Assert.Fail ("Adding pet failed with message: " + exn.Message)

    let pet2 = PetStore.Pet.GetPetById(1337L)
    Assert.AreEqual (pet.Name, pet2.Name)
    Assert.AreEqual (pet.Id, pet2.Id)
//    Assert.AreEqual (pet.Tags, pet2.Tags)
    Assert.AreEqual (pet.Category, pet2.Category)
    Assert.AreEqual (pet.Status, pet2.Status)
//    Assert.AreEqual (pet.PhotoUrls, pet2.PhotoUrls)
    Assert.AreNotEqual (pet, pet2)