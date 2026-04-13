module SwaggerProvider.Tests.Schema_DefinitionPathTests

/// Unit tests for DefinitionPath.Parse — the function that splits a JSON Reference
/// path (e.g. "#/components/schemas/My.Namespace.TypeName") into its namespace list,
/// requested type name, and PascalCase candidate name.

open SwaggerProvider.Internal.Compilers
open Xunit
open FsUnitTyped

// ── Prefix constant ───────────────────────────────────────────────────────────

[<Fact>]
let ``DefinitionPrefix is the OpenAPI component schema reference prefix``() =
    DefinitionPath.DefinitionPrefix |> shouldEqual "#/components/schemas/"

// ── Simple (un-namespaced) names ──────────────────────────────────────────────

[<Fact>]
let ``simple name has empty namespace``() =
    let result = DefinitionPath.Parse "#/components/schemas/Pet"
    result.Namespace |> shouldEqual []

[<Fact>]
let ``simple name preserves RequestedTypeName exactly``() =
    let result = DefinitionPath.Parse "#/components/schemas/Pet"
    result.RequestedTypeName |> shouldEqual "Pet"

[<Fact>]
let ``simple PascalCase name has matching ProvidedTypeNameCandidate``() =
    let result = DefinitionPath.Parse "#/components/schemas/Pet"
    result.ProvidedTypeNameCandidate |> shouldEqual "Pet"

[<Fact>]
let ``simple camelCase name is PascalCased in ProvidedTypeNameCandidate``() =
    let result = DefinitionPath.Parse "#/components/schemas/petModel"
    result.ProvidedTypeNameCandidate |> shouldEqual "PetModel"

[<Fact>]
let ``simple camelCase name preserves original casing in RequestedTypeName``() =
    let result = DefinitionPath.Parse "#/components/schemas/petModel"
    result.RequestedTypeName |> shouldEqual "petModel"

// ── One-level namespaced names ────────────────────────────────────────────────

[<Fact>]
let ``one-level namespace is extracted``() =
    let result = DefinitionPath.Parse "#/components/schemas/My.Pet"
    result.Namespace |> shouldEqual [ "My" ]

[<Fact>]
let ``one-level namespace leaves type name after the dot``() =
    let result = DefinitionPath.Parse "#/components/schemas/My.Pet"
    result.RequestedTypeName |> shouldEqual "Pet"

[<Fact>]
let ``one-level namespace applies PascalCase to ProvidedTypeNameCandidate``() =
    let result = DefinitionPath.Parse "#/components/schemas/my.petModel"
    result.ProvidedTypeNameCandidate |> shouldEqual "PetModel"

// ── Multi-level namespaced names ──────────────────────────────────────────────

[<Fact>]
let ``two-level namespace is fully extracted``() =
    let result = DefinitionPath.Parse "#/components/schemas/A.B.TypeName"
    result.Namespace |> shouldEqual [ "A"; "B" ]
    result.RequestedTypeName |> shouldEqual "TypeName"

[<Fact>]
let ``three-level namespace is fully extracted``() =
    let result = DefinitionPath.Parse "#/components/schemas/A.B.C.TypeName"
    result.Namespace |> shouldEqual [ "A"; "B"; "C" ]
    result.RequestedTypeName |> shouldEqual "TypeName"

[<Fact>]
let ``deep namespace preserves all namespace segments``() =
    let result =
        DefinitionPath.Parse "#/components/schemas/Com.Example.Api.Models.Response"

    result.Namespace |> shouldEqual [ "Com"; "Example"; "Api"; "Models" ]
    result.RequestedTypeName |> shouldEqual "Response"

// ── Names containing non-alphanumeric / non-dot characters ───────────────────
// Hyphens and underscores are valid in JSON schema names but are NOT dot-separators,
// so the function should find no namespace when no dot precedes them.

[<Fact>]
let ``name containing only a hyphen has no namespace``() =
    let result = DefinitionPath.Parse "#/components/schemas/my-type"
    result.Namespace |> shouldEqual []

[<Fact>]
let ``name with hyphen does not extract a spurious namespace``() =
    let result = DefinitionPath.Parse "#/components/schemas/Api.my-type"
    // The dot before "my-type" is in the scanned definition-name segment after
    // the prefix; the hyphen stops the scan, so LastIndexOf('.') finds that dot.
    result.Namespace |> shouldEqual [ "Api" ]

// ── Error handling ────────────────────────────────────────────────────────────

[<Fact>]
let ``definition not starting with prefix throws``() =
    let act = fun () -> DefinitionPath.Parse "notADefinitionPath" |> ignore
    act |> shouldFail

[<Fact>]
let ``swagger 2 definitions path does not start with v3 prefix and throws``() =
    let act = fun () -> DefinitionPath.Parse "#/definitions/Pet" |> ignore
    act |> shouldFail
