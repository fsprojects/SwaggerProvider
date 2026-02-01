module Swagger.NullableDate.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped
open System.Text.Json
open System.Text.Json.Serialization

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v3/nullable-date.yaml"

type TestApi = OpenApiClientProvider<Schema>

[<Fact>]
let ``PersonDto should have nullable birthDate property``() =
    let personType = typeof<TestApi.PersonDto>
    let birthDateProp = personType.GetProperty("BirthDate")
    birthDateProp |> shouldNotEqual null

    // The property should be Option<DateTimeOffset> (default) or Nullable<DateTimeOffset> (with PreferNullable=true)
    let propType = birthDateProp.PropertyType
    propType.IsGenericType |> shouldEqual true

    let genericTypeDef = propType.GetGenericTypeDefinition()

    let hasNullableWrapper =
        genericTypeDef = typedefof<Option<_>>
        || genericTypeDef = typedefof<System.Nullable<_>>

    hasNullableWrapper |> shouldEqual true

[<Fact>]
let ``PersonDto can deserialize JSON with null birthDate using type provider deserialization``() =
    // This JSON is from the issue - a person with null birthDate
    let jsonWithNullBirthDate =
        """{
    "id": "04a38328-4202-44ef-9f2b-ee85b1cd1a48",
    "name": "Test",
    "birthDate": null
}"""

    // Use the same deserialization code as the type provider (System.Text.Json with JsonFSharpConverter)
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())

    // Deserialize - this should not throw
    let person =
        JsonSerializer.Deserialize<TestApi.PersonDto>(jsonWithNullBirthDate, options)

    // Verify the properties
    person.Id |> shouldEqual "04a38328-4202-44ef-9f2b-ee85b1cd1a48"
    person.Name |> shouldEqual "Test"
    person.BirthDate |> shouldEqual None

[<Fact>]
let ``PersonDto can deserialize JSON with valid birthDate using type provider deserialization``() =
    // Test with a valid date value
    let jsonWithValidBirthDate =
        """{
    "id": "test-id-123",
    "name": "John Doe",
    "birthDate": "1990-05-15"
}"""

    // Use the same deserialization code as the type provider
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())

    // Deserialize
    let person =
        JsonSerializer.Deserialize<TestApi.PersonDto>(jsonWithValidBirthDate, options)

    // Verify the properties
    person.Id |> shouldEqual "test-id-123"
    person.Name |> shouldEqual "John Doe"

    // BirthDate should be Some value
    person.BirthDate |> shouldNotEqual None

    match person.BirthDate with
    | Some date ->
        // Verify the date is correct (1990-05-15)
        date.Year |> shouldEqual 1990
        date.Month |> shouldEqual 5
        date.Day |> shouldEqual 15
    | None -> failwith "Expected Some date but got None"
