# SwaggerProvider Repo Assist Notes

## Last Run: 2026-05-20 20:05 UTC (run 26186809474)

## Selected Tasks: 8, 3, 10

### Task 8: Performance Improvements
- DefinitionCompiler: cache `Seq.isEmpty allOf` result (was evaluated twice)
- ProvidedApiClientBase.CallAsync: replace `Array.tryFindIndex((=) codeStr)` with `Array.IndexOf`
- Part of PR (pending): perf-and-tests-20260520

### Task 3: Issue Investigation and Fix → Fallback Task 2
- No fixable bug issues; all issues already have RA comments, no new human activity

### Task 10: Take Repository Forward
- Added 5 new V3 schema compilation tests covering:
  - required vs optional property types
  - string enum compilation (named type + IsEnum)
  - schema description as XmlDoc
- Part of PR (pending): perf-and-tests-20260520
- Total: 438 → 443 tests (+5)

## Comments Made
- Issue #33: commented Apr 2026 (run 23963519508)
- Issue #418: monthly issue (updated each run)

## Open Items Requiring Attention
- Issue #411: CI dead .paket cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked/complex

## Future Work
- Issue #358: Microsoft.OpenApi 3.x migration — revisit when .NET 11 ships
- More V3 schema tests: allOf with multiple $refs, nullable types, nested objects

## Backlog Cursor
- All 4 open issues processed; no new unlabelled issues
