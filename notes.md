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

## Run: 2026-09-09 14:37 UTC (run 34364464627)
### Selected Tasks: 5, 8, 9
- Task 5/8: No new low-risk clearly-beneficial coding/perf improvements found (hot paths
  already heavily optimized from many prior runs). Substituted with Task 9.
- Task 9: `SchemaReader.validateContentType` (SSRF Content-Type allow-list guard, Utils.fs)
  had zero direct unit tests. Added 14 tests to SsrfSecurityTests.fs covering allowed
  media types, charset stripping, case-insensitivity, null handling, html/image rejection,
  and SSRF-disabled bypass. 562/562 tests pass (548->562). Build + fantomas check pass.
  PR: repo-assist/test-validateContentType-coverage (draft)
- Confirmed PRs #486 (eng-bump-deps) and #488 (perf-toStrArray-alloc) merged/closed.
- New issue #490 (OpenApiClientProvider not found on .NET 10.0.400) flagged for future
  triage - related to previously-closed #248 but needs fresh investigation before commenting.
- Task 11: Updated monthly issue #489 with current suggested actions and run history.

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

## Run: 2026-09-09 17:07 UTC (run 34380739176)
### Selected Tasks: 2, 4, 9
- Task 2: Investigated issue #490 (OpenApiClientProvider not found on .NET 10.0.400). Root cause:
  FSharp.Core 10.1.0.0 FileNotFoundException at design-time load. DesignTime.fsproj pins FSharp.Core
  PackageReference Version=8.0.403 (stale vs paket.lock's resolved 10.1.400/10.1.401) with
  ExcludeAssets=runtime;contentFiles - the design-time dll intentionally does NOT ship FSharp.Core.dll,
  relying on the host compiler to supply a compatible version. Posted troubleshooting comment with
  restore/clean/IDE-reload steps and asked for a verbose restore log if unresolved.
  FLAG: consider in a future run whether bumping the 8.0.403 pin to match paket.lock actually changes
  build output (verify with assembly inspection before touching - packaging-critical, don't guess).
- Task 4: `dotnet paket update` found only patch-level bumps (FSharp.Core 10.1.400->10.1.401,
  System.Text.Json/IO.Pipelines/etc 10.0.11->10.0.12, ASP.NET Core 2.3.12->2.3.13). Created PR
  repo-assist/eng-bump-deps-20260909. Build succeeded, 562/562 unit tests pass.
- Task 9: `Caching.createInMemoryCache` (Caching.fs) had zero unit tests despite being used by
  Provider.OpenApiClient.fs to cache generated provided types. Added 11 tests in new CachingTests.fs
  (Set/TryRetrieve/Remove/GetOrAdd, per-key isolation, expiration, extendCacheExpiration). Build +
  fantomas + 573/573 tests pass (562->573). Created draft PR repo-assist/test-caching-coverage.
- Task 11: Updated monthly issue #489 - added new run entry, added 2 new PRs and 1 check-comment
  item (#490) to suggested actions, kept #411 close-issue item and future-work notes.
