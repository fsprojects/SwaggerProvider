namespace SwaggerProvider.Tests.UtilsTests

open Xunit
open FsUnitTyped
open SwaggerProvider.Internal

/// Unit tests for UniqueNameGenerator — used by all DefinitionCompilers and OperationCompilers
/// to de-duplicate property and method names within a given type scope.
module UniqueNameGeneratorTests =

    [<Fact>]
    let ``first use of a name is returned unchanged``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Foo" |> shouldEqual "Foo"

    [<Fact>]
    let ``second use of same name gets numeric suffix 1``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Bar" |> ignore
        gen.MakeUnique "Bar" |> shouldEqual "Bar1"

    [<Fact>]
    let ``third use of same name gets numeric suffix 2``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Bar" |> ignore
        gen.MakeUnique "Bar" |> ignore
        gen.MakeUnique "Bar" |> shouldEqual "Bar2"

    [<Fact>]
    let ``collision detection is case-insensitive``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Foo" |> ignore
        // "foo" collides with "Foo" because comparison is case-insensitive
        gen.MakeUnique "foo" |> shouldEqual "foo1"

    [<Fact>]
    let ``original casing of the returned name is preserved``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "MyProperty" |> shouldEqual "MyProperty"

    [<Fact>]
    let ``suffix casing follows the input not the stored key``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "myMethod" |> ignore
        // Suffix is appended to the original input, preserving its casing
        gen.MakeUnique "myMethod" |> shouldEqual "myMethod1"

    [<Fact>]
    let ``different names do not collide``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Alpha" |> shouldEqual "Alpha"
        gen.MakeUnique "Beta" |> shouldEqual "Beta"
        gen.MakeUnique "Gamma" |> shouldEqual "Gamma"

    [<Fact>]
    let ``numeric suffixes increment sequentially``() =
        let gen = UniqueNameGenerator()
        let names = [ for _ in 0..4 -> gen.MakeUnique "X" ]
        names |> shouldEqual [ "X"; "X1"; "X2"; "X3"; "X4" ]

    [<Fact>]
    let ``name equal to a previously suffixed name is also de-duplicated``() =
        // After generating "Op" and "Op1", adding "Op1" should produce "Op11"
        let gen = UniqueNameGenerator()
        gen.MakeUnique "Op" |> ignore // reserves "op"
        gen.MakeUnique "Op" |> ignore // reserves "op1"
        gen.MakeUnique "Op1" |> shouldEqual "Op11" // "op1" is taken → try "op11"

    [<Fact>]
    let ``empty string is accepted as input``() =
        let gen = UniqueNameGenerator()
        gen.MakeUnique "" |> shouldEqual ""
        gen.MakeUnique "" |> shouldEqual "1"
