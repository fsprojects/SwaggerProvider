#r "nuget: SwaggerProvider, 1.0.0-beta2"

open SwaggerProvider

[<Literal>]
let Schema = "https://petstore.swagger.io/v2/swagger.json"

type PetStore = OpenApiClientProvider<Schema>
let petStoreClient = PetStore.Client()

let inventory =
    petStoreClient.GetInventory()
    |> Async.AwaitTask
    |> Async.RunSynchronously


let tag = PetStore.Tag()
tag.Id <- Some 1337L
tag.Name <- "foobar"

tag.ToString()

let category = PetStore.Category()
category.Id <- Some 1337L
category.Name <- "dog"

category.ToString()

let pet = PetStore.Pet(Name = "foo", Id = Some 1337L)
pet.Name <- "bar" // Overwrites "foo"
pet.Category <- category
pet.Status <- "sold"
pet.PhotoUrls <- [||]
pet.Tags <- [| tag |]

pet.ToString()


let dic = [ 1, "2"; 3, "4" ] |> Map.ofList


type V(x: int) =
    member __.X = x
    override __.ToString() = "hello"

let v = V(5)
v
v.ToString()

let o = v :> obj
o.ToString()


let arr = [| tag |]
let arrObj = arr :> obj

let ty = arrObj.GetType()

ty.IsArray
