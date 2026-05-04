namespace SwaggerProvider.Tests.UtilsTests

open System.Collections.Generic
open System.Text.Json.Nodes
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

    [<Fact>]
    let ``occupied names seed prevents first-use from returning the reserved name unchanged``() =
        let gen = UniqueNameGenerator(occupiedNames = [ "Foo" ])
        gen.MakeUnique "Foo" |> shouldEqual "Foo1"

    [<Fact>]
    let ``occupied names seed is case-insensitive``() =
        let gen = UniqueNameGenerator(occupiedNames = [ "foo" ])
        gen.MakeUnique "Foo" |> shouldEqual "Foo1"

    [<Fact>]
    let ``multiple occupied names are all reserved``() =
        let gen = UniqueNameGenerator(occupiedNames = [ "Alpha"; "Beta" ])
        gen.MakeUnique "Alpha" |> shouldEqual "Alpha1"
        gen.MakeUnique "Beta" |> shouldEqual "Beta1"
        gen.MakeUnique "Gamma" |> shouldEqual "Gamma"

    [<Fact>]
    let ``empty occupied names sequence behaves like default constructor``() =
        let gen = UniqueNameGenerator(occupiedNames = [])
        gen.MakeUnique "Foo" |> shouldEqual "Foo"

// ── XmlDoc.buildEnumDoc ───────────────────────────────────────────────────────

/// Direct unit tests for the XmlDoc.buildEnumDoc helper, which converts a list of
/// JsonNode enum values into a human-readable "Allowed values: …" string.
module BuildEnumDocTests =

    [<Fact>]
    let ``null enum list returns None``() =
        XmlDoc.buildEnumDoc null |> shouldEqual None

    [<Fact>]
    let ``empty enum list returns None``() =
        let empty = List<JsonNode>() :> IList<JsonNode>
        XmlDoc.buildEnumDoc empty |> shouldEqual None

    [<Fact>]
    let ``single string enum value produces Allowed values line``() =
        let values =
            List<JsonNode>([| JsonValue.Create("active") :> JsonNode |]) :> IList<JsonNode>

        let doc = XmlDoc.buildEnumDoc values
        doc |> shouldEqual(Some "Allowed values: active")

    [<Fact>]
    let ``multiple string enum values are comma-separated``() =
        let values =
            List<JsonNode>(
                [| JsonValue.Create("active") :> JsonNode
                   JsonValue.Create("inactive") :> JsonNode
                   JsonValue.Create("pending") :> JsonNode |]
            )
            :> IList<JsonNode>

        let doc = XmlDoc.buildEnumDoc values
        doc.IsSome |> shouldEqual true
        doc.Value |> shouldContainText "Allowed values:"
        doc.Value |> shouldContainText "active"
        doc.Value |> shouldContainText "inactive"
        doc.Value |> shouldContainText "pending"

    [<Fact>]
    let ``integer enum values appear as numbers``() =
        let values =
            List<JsonNode>([| JsonValue.Create(1) :> JsonNode; JsonValue.Create(2) :> JsonNode |]) :> IList<JsonNode>

        let doc = XmlDoc.buildEnumDoc values
        doc.IsSome |> shouldEqual true
        doc.Value |> shouldContainText "1"
        doc.Value |> shouldContainText "2"

    [<Fact>]
    let ``null json node in enum list renders as null``() =
        let values = List<JsonNode>([| null |]) :> IList<JsonNode>
        let doc = XmlDoc.buildEnumDoc values
        doc.IsSome |> shouldEqual true
        doc.Value |> shouldContainText "null"

// ── XmlDoc.combineDescAndEnum ─────────────────────────────────────────────────

/// Direct unit tests for XmlDoc.combineDescAndEnum which merges a schema description
/// with optional enum documentation.
module CombineDescAndEnumTests =

    [<Fact>]
    let ``null description and None enum returns null``() =
        XmlDoc.combineDescAndEnum null None |> shouldEqual null

    [<Fact>]
    let ``empty description and None enum returns null``() =
        XmlDoc.combineDescAndEnum "" None |> shouldEqual null

    [<Fact>]
    let ``whitespace-only description and None enum returns null``() =
        XmlDoc.combineDescAndEnum "   " None |> shouldEqual null

    [<Fact>]
    let ``description only is returned unchanged``() =
        XmlDoc.combineDescAndEnum "My description" None
        |> shouldEqual "My description"

    [<Fact>]
    let ``enum doc only is returned when description is null``() =
        XmlDoc.combineDescAndEnum null (Some "Allowed values: a, b")
        |> shouldEqual "Allowed values: a, b"

    [<Fact>]
    let ``both description and enum doc are combined with newline``() =
        let result = XmlDoc.combineDescAndEnum "The status" (Some "Allowed values: a, b")
        result |> shouldContainText "The status"
        result |> shouldContainText "Allowed values: a, b"

// ── XmlDoc.buildXmlDoc ────────────────────────────────────────────────────────

/// Direct unit tests for XmlDoc.buildXmlDoc — the central doc-string builder
/// for summary, remarks, parameter, and returns tags.
module BuildXmlDocTests =

    [<Fact>]
    let ``summary only produces summary tag``() =
        let doc = XmlDoc.buildXmlDoc "Get pet" null [] None
        doc |> shouldContainText "<summary>Get pet</summary>"

    [<Fact>]
    let ``description equal to summary does not produce remarks tag``() =
        let doc = XmlDoc.buildXmlDoc "Get pet" "Get pet" [] None
        doc |> shouldNotContainText "<remarks>"

    [<Fact>]
    let ``description different from summary produces remarks tag``() =
        let doc = XmlDoc.buildXmlDoc "Get pet" "Returns the pet by ID" [] None
        doc |> shouldContainText "<summary>Get pet</summary>"
        doc |> shouldContainText "<remarks>Returns the pet by ID</remarks>"

    [<Fact>]
    let ``null description does not produce remarks tag``() =
        let doc = XmlDoc.buildXmlDoc "Get pet" null [] None
        doc |> shouldNotContainText "<remarks>"

    [<Fact>]
    let ``param descriptions appear as param tags``() =
        let doc = XmlDoc.buildXmlDoc "Op" null [ ("petId", "The pet identifier") ] None

        doc
        |> shouldContainText "<param name=\"petId\">The pet identifier</param>"

    [<Fact>]
    let ``null param description is omitted``() =
        let doc = XmlDoc.buildXmlDoc "Op" null [ ("petId", null) ] None
        doc |> shouldNotContainText "<param"

    [<Fact>]
    let ``returns tag appears when returnDoc is Some``() =
        let doc = XmlDoc.buildXmlDoc "Op" null [] (Some "The pet")
        doc |> shouldContainText "<returns>The pet</returns>"

    [<Fact>]
    let ``returns tag is absent when returnDoc is None``() =
        let doc = XmlDoc.buildXmlDoc "Op" null [] None
        doc |> shouldNotContainText "<returns>"

    [<Fact>]
    let ``ampersand in summary is XML-escaped``() =
        let doc = XmlDoc.buildXmlDoc "Cats & Dogs" null [] None
        doc |> shouldContainText "Cats &amp; Dogs"
        doc |> shouldNotContainText "Cats & Dogs"

    [<Fact>]
    let ``less-than in description is XML-escaped``() =
        let doc = XmlDoc.buildXmlDoc "Op" "Value < threshold" [] None
        doc |> shouldContainText "&lt;"
        doc |> shouldNotContainText "Value < threshold"

    [<Fact>]
    let ``greater-than in description is XML-escaped``() =
        let doc = XmlDoc.buildXmlDoc "Op" "Value > threshold" [] None
        doc |> shouldContainText "&gt;"
        doc |> shouldNotContainText "Value > threshold"

    [<Fact>]
    let ``empty summary and null description and no params returns empty string``() =
        let doc = XmlDoc.buildXmlDoc "" null [] None
        doc |> shouldEqual ""
