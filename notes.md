# SwaggerProvider Repo Assist Notes

## Last Run: 2026-05-27 20:05 UTC (run 26535594763)

## Selected Tasks: 5, 1 (→Task 2 fallback), 6

### Task 5: Coding Improvements
- Fixed empty Cookie header bug in OperationCompiler.fs
  - OperationCompiler always prepended ("Cookie", "") to headers even with no cookie params
  - fillHeaders only filters null, not empty strings → spurious Cookie: header on every request
  - Fix: only prepend Cookie header when cookieHeader is non-empty
- Part of PR (pending): improve-cookie-header-20260527 (PR #454)
- Tests: 465/465 pass

### Task 1 → Fallback Task 2
- All 4 open issues have RA comments, no new human activity

### Task 6: Maintain Repo Assist PRs
- PR #452: CI failures are pre-existing on master (ProvidedTypes.fs regression from ceeb6bc)
  - Commented to explain
- PR #453: CI failures are pre-existing on master
  - Commented to explain

## Infrastructure Issue Found
- `InvalidProgramException` at PetStoreNullable.Tag.set_Name in integration tests
- Regression from paket update (commit e048624, May 25)
- ProvidedTypes.fs bumped from a54d92b to ceeb6bc
- Affects master and all open PRs
- Added to monthly summary Suggested Actions

## Comments Made
- Issue #33: commented Apr 2026 (run 23963519508)
- Issue #418: monthly issue (updated each run)
- PR #452: CI comment (run 26535594763)
- PR #453: CI comment (run 26535594763)

## Open Items Requiring Attention
- Issue #411: CI dead .paket cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked/complex
- Integration test regression: InvalidProgramException (ProvidedTypes.fs ceeb6bc)

## Future Work
- Issue #358: Microsoft.OpenApi 3.x migration — revisit when .NET 11 ships
- More V3 schema tests: allOf with multiple $refs, nullable types, nested objects

## Backlog Cursor
- All 4 open issues processed; no new unlabelled issues
