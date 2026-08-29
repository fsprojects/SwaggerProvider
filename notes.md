# SwaggerProvider Repo Assist Notes

## Last Run: 2026-07-28 12:00 UTC (run 30356342841)

## Selected Tasks: 5, 1, 3

### Task 1: Issue Labelling
- Labelled #477 with `bug`, `needs investigation`

### Task 3: Issue Fix — Named object component aliases emit duplicate ProvidedTypeDefinition (#477)
- Bug: `registerInNsAndInDef` re-registered alias PTDs in the namespace, causing
  "duplicate entry '<name>' in type index table" during assembly emit.
- Fix: before calling `ns.RegisterType`, check via reference equality if PTD is
  already in `pathToType.Values`. If so (alias path), skip namespace registration.
- Added 4 regression tests (547→551 total).
- PR: branch repo-assist/fix-issue-477-named-alias-duplicate-type (draft)

### Task 5 (subsumed into Task 3)
- The fix also covers latent duplicate in allOf/anyOf/oneOf single-ref cases

### Task 11: Monthly Activity Summary
- Updated issue #467 (July 2026 monthly)
- Added new run entry and new PR to suggested actions

## Infrastructure Notes
- Issue #411: dead .paket CI cache step — requires manual PR (protected workflow files)
- Issue #358: Microsoft.OpenApi 3.x migration — blocked, revisit with .NET 11
- PR #474: Dependabot actions/checkout 7.0.0→7.0.1 — awaiting maintainer review

## Open PRs (Repo Assist)
- fix-issue-477: fix duplicate ProvidedTypeDefinition for named component aliases (2026-07-28)

## Comments Made
- Issue #33: Apr 2026 (no new human activity)
- Issue #358: Apr 2026 (no new human activity)
- Issue #477: 2026-07-28 (new issue, root cause confirmed)

## Recent History
- v4.1.0 released June 2026
- PR #473 (fix formatObject Option<DateOnly>/Option<TimeOnly> scalars): merged July 2026
- PRs #471, #472, #474 (Dependabot GH Actions): #471,#472 merged; #474 awaiting review
- PR #aw_fmtarr_fix (fix formatObject for Option<T> array elements): check if merged

## Backlog Cursor
- issue_backlog_cursor: 477 (all open issues processed this run)

## Run: 2026-08-29 02:26 UTC (run 33228882898)
### Selected Tasks: 3, 2, 4
- Task 3: No fixable bug/help-wanted/good-first-issue issues found.
- Task 2: No new human activity on #33/#358; skipped to avoid spam.
- Task 4: Created PR repo-assist/eng-bump-deps-20260829 — `dotnet paket update` refresh of paket.lock (Microsoft.OpenApi 2.7.5->2.12.2, FSharp.Core, SharpYaml, xunit v3, test tooling). Build + 548 unit tests pass. Integration tests blocked by sandbox proxy (pre-existing, confirmed on master too).
### Task 11: Closed July monthly #467, created new August 2026 monthly activity issue.
