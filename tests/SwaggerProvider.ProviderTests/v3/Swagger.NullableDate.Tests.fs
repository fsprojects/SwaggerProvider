module Swagger.NullableDate.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/nullable-date.yaml"

type TestApi = OpenApiClientProvider<Schema>

[<Fact>]
let ``PersonDto should have nullable birthDate property``() =
    let personType = typeof<TestApi.PersonDto>
    let birthDateProp = personType.GetProperty("BirthDate")
    birthDateProp |> shouldNotEqual null
    
    // The property should be Option<DateTimeOffset> or Nullable<DateTimeOffset>
    let propType = birthDateProp.PropertyType
    propType.IsGenericType |> shouldEqual true
    
    let genericTypeDef = propType.GetGenericTypeDefinition()
    let isOptionOrNullable = 
        genericTypeDef = typedefof<Option<_>> || genericTypeDef = typedefof<System.Nullable<_>>
    
    isOptionOrNullable |> shouldEqual true
    "BirthDate should be wrapped as Option or Nullable because it's marked as nullable in the schema" |> ignore
