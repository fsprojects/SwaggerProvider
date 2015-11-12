// Load SwaggerProvider
#r @"../../src/SwaggerProvider/bin/Release/SwaggerProvider.dll"
open SwaggerProvider

// Petstore
[<Literal>]
let path = __SOURCE_DIRECTORY__ + "/../SwaggerProvider.Tests/Schemas/PetStore.Swagger.json"
type PetStore = SwaggerProvider<path, "Content-Type,application/json">

// Types
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

// Calls
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

// Serialization
#r @"../../packages/Newtonsoft.Json.7.0.1/lib/net40/Newtonsoft.Json.dll"

open SwaggerProvider.OptionConverter
open Newtonsoft.Json
open Newtonsoft.Json.Converters

let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore) //Add 'Formatting = Formatting.Indented' for nice json formatting
settings.Converters.Add(new OptionConverter () :> Newtonsoft.Json.JsonConverter) // This does not run if the session is not bound to Blend/Newtonsoft.Json.dll
let body = JsonConvert.SerializeObject(pet :> obj, settings)
let data = body.ToLower ()
data

// HTTP calls
open FSharp.Data
Http.RequestString("http://petstore.swagger.io/v2/pet/6?=", headers = seq [("Content-Type","application/json")], query = [])
Http.RequestString("http://petstore.swagger.io/v2/pet", httpMethod = "POST", headers = seq [("Content-Type","application/json")], body = HttpRequestBody.TextRequest data, query = [])
