# SwaggerProvider Repo Assist Notes

## Last Run: 2026-06-01 20:45 UTC (run 26780901012)

## Selected Tasks: 9, 4 (→Task 5 fallback), 8

### Task 9: Testing Improvements
- Added 9 new tests (500→509):
  - RuntimeHelpersTests: 4 tests for RFC 3986 percent-encoding in createHttpRequest
    (spaces→%20, special chars in values/names)
  - Schema.OperationCompilationTests: 2 tests - 200 wins over 201 when both defined
  - Schema.V2SchemaCompilationTests: 4 tests - V2 operation return types:
    listPets→Task<Pet[]>, getPet→Task<Pet>, getPet path param int64,
    createPet→Task<IO.Stream> (documents Microsoft.OpenApi normalization behavior)

### Task 4 → Task 5: Coding Improvement
- toFormUrlEncodedContent: merged Seq.filter+choose into single Seq.choose pass
  (avoids intermediate sequence allocation)

### Task 8: Performance
- createHttpRequest: replaced UriBuilder+ParseQueryString+NameValueCollection
  with StringBuilder+Uri.EscapeDataString
  - Reduces ~5 allocations per API call to 1
  - Encoding: %20 for spaces (RFC 3986) instead of + (form-encoding)
  - Removed System.Web dependency

### Task 11: Monthly Activity Summary
- Closed May 2026 monthly issue #418
- Created June 2026 monthly issue (PR pending push for exact number)

## Infrastructure Notes
- Issue #411: dead .paket CI cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked/complex, revisit with .NET 11

## Open Items
- June monthly issue just created (number unknown until push)
- PR pending: perf+test StringBuilder query builder (branch: repo-assist/test-perf-june-20260601)

## Comments Made
- Issue #33: commented Apr 2026
- Issue #418: monthly issue (now closed)
- PRs #452,#453: CI comments (run 26535594763)

## Recent History
- v4.1.0 released (tag 7de7d9a)
- PR #455 by maintainer: reduce allocations in generated operation code
- All May RA PRs (#450,#452,#453,#454) merged
- schedule changed to weekly (commit 682c5c5)

## Backlog Cursor
- All 4 open issues processed; no new unlabelled issues
- Open issues: #33 (Feature Request), #358 (OpenApi 3.x), #411 (dead cache step)
