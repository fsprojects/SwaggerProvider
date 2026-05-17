# SwaggerProvider Repo Assist Notes

## Last Run: 2026-05-17 19:47 UTC (run 26000869764)

## Selected Tasks: 8, 5, 10

### Task 8: Performance Improvements
- Refactored `formatObject` in RuntimeHelpers.fs to use StringBuilder
- Avoids intermediate string[] array and multiple per-property string allocations
- PR pending

### Task 5: Coding Improvements
- Simplified the allOf/oneOf/anyOf resolvedType block in DefinitionCompiler.fs
- Extracted `tryResolveSingle` helper + Option.orElseWith chaining
- Reduced ~25 lines of repetitive if/else to ~12 lines
- PR pending

### Task 10: Testing Improvements
- Added 8 new tests in RuntimeHelpersTests.fs (417→425)
- formatObject: null array element, mixed null/non-null array, empty object
- toParam: int64, bool true/false fast-paths, float32/double generic fallback
- PR pending

## Comments Made
- Issue #33: commented Apr 2026 (run 23963519508)
- Issue #418: monthly issue (updated each run)

## Open Items Requiring Attention
- Issue #411: CI dead .paket cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked/complex

## Future Work
- Issue #358: Microsoft.OpenApi 3.x migration — revisit when .NET 11 ships
- Consider 4.0.0 release when beta04 feedback received (do NOT create release notes PRs)

## Backlog Cursor
- All 4 open issues processed; no new unlabelled issues
