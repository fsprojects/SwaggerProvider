#r "nuget: SwaggerProvider"
open SwaggerProvider

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/Schemas/v3/nullable-date.yaml"

type TestApi = OpenApiClientProvider<Schema>

// Check the type of BirthDate property
let personType = typeof<TestApi.PersonDto>
let birthDateProp = personType.GetProperty("BirthDate")

printfn "BirthDate property type: %A" birthDateProp.PropertyType
printfn "Is generic: %b" birthDateProp.PropertyType.IsGenericType

if birthDateProp.PropertyType.IsGenericType then
    let genericTypeDef = birthDateProp.PropertyType.GetGenericTypeDefinition()
    printfn "Generic type definition: %A" genericTypeDef
    printfn "Is Option: %b" (genericTypeDef = typedefof<Option<_>>)
    printfn "Is Nullable: %b" (genericTypeDef = typedefof<System.Nullable<_>>)
