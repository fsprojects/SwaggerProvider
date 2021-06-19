using System;
using PS = Swagger.PetStore.Tests.PetStoreNullable;

namespace SwaggerProvider.ProviderTests.CSharp
{
    class MainClass
    {
        public static void UsePetstore()
        {
            var tag = new PS.Tag(null, "foo");
            var tag2 = new PS.Tag { Name = "foo" };

            if (!tag.ToString().Contains("foo"))
                throw new Exception("Invalid ToString implementation - no `foo` found");

            // TODO: Check why params are not optional - where is default values
            var pet = new PS.Pet("foo", new string[0], 1337, null, null, null);
            var pet2 = new PS.Pet { Name = "foo", Id = 1337L };

            if (!tag.ToString().Contains("1337"))
                throw new Exception("Invalid ToString implementation - no `1337` found");

            var client = new PS.Client();
            client.AddPet(pet2);
        }

        public static void Main(string[] args)
        {
            UsePetstore();
        }
    }
}
