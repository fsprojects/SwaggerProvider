# Swagger Client Provider

<Note type="warning">

The SwaggerClientProvider is outdated. There is no plans to improve custom Swagger 2.0 schema parser or bring new features to this type provider. We hope to remove it from source code when users migrate to [OpenApiClientProvider](/OpenApiClientProvider) and OpenApi 3.0 schemas.

</Note>

SwaggerClientProvider is generative F# Type Provider, build on top of custom Swagger schema parser that supports **only** 2.0 schema format.

```fsharp
open SwaggerProvider

let [<Literal>]schema = "http://petstore.swagger.io/v2/swagger.json"
type PetStore = SwaggerClientProvider<schema>
let petStore = PetStore.Client()
```

## Parameters

When you use TP you can specify following parameters

| Parameter | Description |
|-----------|-------------|
| `Schema` | Url or Path to Swagger schema file. |
| `Headers` | HTTP Headers requiried to access the schema. |
| `IgnoreOperationId` | Do not use `operationsId` and generate method names using `path` only. Default value `false`. |
| `IgnoreControllerPrefix` | Do not parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`. |
| `PreferNullable` | Provide `Nullable<_>` for not required properties, instead of `Option<_>`. Defaults value `false`. |
| `PreferAsync` | Generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults value `false`. |

More configuration scenarios are described in [Customization section](/Customization)

## Sample

<Note type="warning">

The rest of the page is outdated

</Note>

Instantiate the types provided by the SwaggerProvider.

```fsharp
let tag = PetStore.Tag()
tag.Id <- Some 1337L
tag.Name <- "foobar"

let category = new PetStore.Category()
category.Id <- Some 1337L
category.Name <- "dog"

let pet = new PetStore.Pet (Name = "foo", Id = Some 1337L)
pet.Name <- "bar" // Overwrites "foo"
pet.Category <- category
pet.Status <- "sold"
pet.Tags <- [|tag|]

let user = new PetStore.User()
user.Id <- Some 1337L
user.FirstName <- "Firstname"
user.LastName <- "Lastname"
user.Email <- "e-mail"
user.Password <- "password"
user.Phone <- "12345678"
user.Username <- "user_name"
```

Invoke the Swagger operations using `petStore` instance.

```fsharp
async {
    let! f = petStore.GetPetById(6L)
    f.Category <- PetStore.Category(Id = Some 1337L, Name = "dog")
    f.Name <- "Hans"
    f.Tags <- Array.append f.Tags [|tag|]

    let! pet = petStore.AddPet(pet)
    let! x = petStore.FindPetsByTags([|"tag1"|])
    Array.length x

    do! petStore.UpdatePetWithForm(-1L, "name", "sold")
    let! leetPet = petStore.GetPetById(1337L)

    let! h = petStore.FindPetsByStatus([|"pending";"sold"|])
    h.ToString()
    let! i = petStore.FindPetsByTags([|"tag2"|])
    i.ToString()

    let! inventory = petStore.GetInventory()
    inventory.ToString()
    let! order = petStore.GetOrderById(3L)
    order.ToString()

    let! ``14`` = petStore.GetPetById(14L)
    ``14``.ToString()
    do! petStore.DeletePet(14L, "no-key")
    // throws, the pet no longer exists!
    let! pet = petStore.GetPetById(14L)

    let! ``14`` = PetStore.Pet.GetPetById(14L)
    ``14``.ToString()
    // throws, the pet no longer exists!
    do! PetStore.Pet.DeletePet(14L, "no-key")
    let! ``14`` = PetStore.Pet.GetPetById(14L)
    ``14``.ToString()
}
```
