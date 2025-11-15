## Build, Test & Lint Commands

- **Build**: `dotnet fake build -t Build` (Release configuration)
- **Format Check**: `dotnet fake build -t CheckFormat` (validates Fantomas formatting)
- **Format**: `dotnet fake build -t Format` (applies Fantomas formatting)
- **All Tests**: `dotnet fake build -t RunTests` (builds + starts test server + runs all tests)
- **Unit Tests Only**: `dotnet build && dotnet tests/SwaggerProvider.Tests/bin/Release/net10.0/SwaggerProvider.Tests.dll`
- **Provider Tests (Integration)**:
  1. Build test server: `dotnet build tests/Swashbuckle.WebApi.Server/Swashbuckle.WebApi.Server.fsproj -c Release`
  2. Start server in background: `dotnet tests/Swashbuckle.WebApi.Server/bin/Release/net10.0/Swashbuckle.WebApi.Server.dll`
  3. Build tests: `dotnet build SwaggerProvider.TestsAndDocs.sln -c Release`
  4. Run tests: `dotnet tests/SwaggerProvider.ProviderTests/bin/Release/net10.0/SwaggerProvider.ProviderTests.dll`
- **Single Test**: Run via xunit runner: `dotnet [assembly] [filter]`

## Code Style Guidelines

**Language**: F# (net10.0 target framework)

**Imports & Namespaces**:

- `namespace [Module]` at file start; no `open` statements at module level
- Use `module [Name]` for nested modules
- Open dependencies after namespace declaration (e.g., `open Xunit`, `open FsUnitTyped`)
- Fully qualify internal modules: `SwaggerProvider.Internal.v2.Parser`, `SwaggerProvider.Internal.v3.Compilers`

**Formatting** (via Fantomas, EditorConfig enforced):

- 4-space indents, max 150 char line length
- `fsharp_max_function_binding_width=10`, `fsharp_max_infix_operator_expression=70`
- No space before parameter/lowercase invocation
- Multiline block brackets on same column, Stroustrup style enabled
- Bar before discriminated union declarations, max 3 blank lines

**Naming Conventions**:

- PascalCase for classes, types, modules, public members
- camelCase for local/private bindings, parameters
- Suffix test functions with `Tests` or use attributes like `[<Theory>]`, `[<Fact>]`

**Type Annotations**:

- Explicit return types for public functions (recommended)
- Use type inference for local bindings when obvious
- Generic type parameters: `'a`, `'b` (single quote prefix)

**Error Handling**:

- Use `Result<'T, 'Error>` or `Option<'T>` for fallible operations
- `failwith` or `failwithf` for errors in type providers and compilers
- Task-based async for I/O: `task { }` expressions in tests
- Match failures with `| _ -> ...` or pattern guards with `when`

**File Organization**:

- Tests use Xunit attributes: `[<Theory>]`, `[<Fact>]`, `[<MemberData>]`
- Design-time providers in `src/SwaggerProvider.DesignTime/`, runtime in `src/SwaggerProvider.Runtime/`
- Test schemas organized by OpenAPI version: `tests/.../Schemas/{v2,v3}/`

## Key Patterns

- Type Providers use `ProvidedApiClientBase` and compiler pipeline (DefinitionCompiler, OperationCompiler)
- SSRF protection enabled by default; disable with `SsrfProtection=false` static parameter
- Target net10.0; use implicit async/await (task expressions)
