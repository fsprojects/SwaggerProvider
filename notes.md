# SwaggerProvider Repo Assist Notes

## Last Run: 2026-05-19 20:02 UTC (run 26121890756)

## Selected Tasks: 9, 10, 8

### Task 8: Performance Improvements
- Replaced `GetCustomAttributes(type, bool)` array-pattern in `getPropertyNamesAndInfos`
  with `Attribute.GetCustomAttribute(prop, type)` to avoid `obj[]` allocation per property
- Part of PR (pending): perf/test improvements-20260519

### Task 9: Testing Improvements
- Added 8 new tests to `CreateHttpRequestTests` (PATCH, HEAD, OPTIONS, TRACE, custom method, case-insensitive, multi-param)
- Part of PR (pending): perf/test improvements-20260519

### Task 10: Take Repository Forward
- Added 6 tests for `tryResolveSingle` allOf/oneOf/anyOf single-$ref resolution
  in Schema.V2SchemaCompilationTests.fs
- Part of PR (pending): perf/test improvements-20260519
- Total: 425 → 438 tests (+13)

## Comments Made
- Issue #33: commented Apr 2026 (run 23963519508)
- Issue #418: monthly issue (updated each run)

## Open Items Requiring Attention
- Issue #411: CI dead .paket cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked/complex

## Future Work
- Issue #358: Microsoft.OpenApi 3.x migration — revisit when .NET 11 ships

## Backlog Cursor
- All 4 open issues processed; no new unlabelled issues
