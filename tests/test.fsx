#I "../bin/SwaggerProvider"
#r "SwaggerProvider.dll"
#r "SwaggerProvider.Runtime.dll"

open SwaggerProvider
let [<Literal>]Schema = "http://petstore.swagger.io/v2/swagger.json"
type PetStore = SwaggerProvider<Schema> // Provided Types
let petStore = PetStore()

let tag = PetStore.Tag()
tag.Id <- Some 1337L
tag.Name <- "foobar"

tag.ToString()

let category = PetStore.Category()
category.Id <- Some 1337L
category.Name <- "dog"

category.ToString()

let pet = PetStore.Pet (Name = "foo", Id = Some 1337L)
pet.Name <- "bar" // Overwrites "foo"
pet.Category <- category
pet.Status <- "sold"
pet.PhotoUrls <- [||]
pet.Tags <- [|tag|]

pet.ToString()


let dic = [1,"2"; 3,"4"] |> Map.ofList


type V (x:int) =
    member __.X = x
    override __.ToString() =
        "hello"

let v = V(5)
v
v.ToString()

let o = v :> obj
o.ToString()


let arr = [|tag|]
let arrObj = arr :> obj

let ty = arrObj.GetType()

ty.IsArray




let [<Literal>]Schema' = "https://raw.githubusercontent.com/Krzysztof-Cieslak/SwaggerProviderOptionalQuery/master/swagger.json"
type MyTP = SwaggerProvider<Schema'> // Provided Types
let tp = MyTP()



tp.GetAccounts()


type WebAPI = SwaggerProvider<"http://localhost:8735/swagger/docs/v1", IgnoreOperationId=true>
let api = WebAPI()

api.GetApiUpdateWithOptionalInt(1, Some(10))