#r @"..\..\src\SwaggerProvider\bin\Debug\SwaggerProvider.dll"
open SwaggerProvider

type PetStore = SwaggerProvider< @"D:\Personal\GitHub\SwaggerProvider\tests\SwaggerProvider.Tests\Schemas\PetStore.Swagger.json">

PetStore.Definitions.