(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.

// This script will not run since this file is not placed in the folder 
// SwaggerProvider/test/SwaggerProvider.Tests where script.fsx is placed.
// However, it is not meant to as it is only used to generate the Tutorial.html

(**
SwaggerProvider Tutorial
========================

Based on the script file "SwaggerProvider/tests/SwaggerProvider.Tests/script.fsx".
Change the paths to match your own directories, if you do not use the entire github repository.

Start by loading the swagger provider.
*)

#r @"../../src/SwaggerProvider/bin/Release/SwaggerProvider.dll"
open SwaggerProvider

[<Literal>]
let path = __SOURCE_DIRECTORY__ + "/Schemas/PetStore.Swagger.json"
type PetStore = SwaggerProvider<path, "Content-Type,application/json">

(**
Instantiate the types provided by the SwaggerProvider.
*)

let tag = PetStore.Definitions.Tag()
tag.Id <- Some 1337L
tag.Name <- "foobar"

let category = new PetStore.Definitions.Category()
category.Id <- Some 1337L
category.Name <- "dog"

let pet = new PetStore.Definitions.Pet (Name = "foo", Id = Some 1337L)
pet.Name <- "bar" // Overwrites "foo"
pet.Category <- category
pet.Status <- "sold"
pet.Tags <- [|tag|]

let user = new PetStore.Definitions.User()
user.Id <- Some 1337L
user.FirstName <- "Firstname"
user.LastName <- "Lastname"
user.Email <- "e-mail"
user.Password <- "password"
user.Phone <- "12345678"
user.Username <- "user_name"

(**
Invoke the Swagger operations. Beware that the PetStore is publicly available and may be changed by anyone.
*)

let f = PetStore.Pet.GetPetById(6L)
f.Category
f.Name <- "Hans"
f.Tags <- Array.append f.Tags [|tag|]

PetStore.Pet.AddPet(pet)
let x = PetStore.Pet.FindPetsByTags([|"tag1"|])
Array.length x

PetStore.Pet.UpdatePetWithForm(-1L, "name", "sold")
PetStore.Pet.GetPetById(1337L)

let h = PetStore.Pet.FindPetsByStatus([|"pending";"sold"|])
h.ToString()
let i = PetStore.Pet.FindPetsByTags([|"tag2"|])
i.ToString()

PetStore.Store.GetInventory().ToString()
PetStore.Store.GetOrderById(3L).ToString()

PetStore.Pet.GetPetById(14L).ToString()
PetStore.Pet.DeletePet(14L, "no-key").ToString()
PetStore.Pet.GetPetById(14L).ToString()

(**
Enjoy!
*)
