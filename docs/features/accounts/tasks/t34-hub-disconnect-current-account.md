---
id: T34
title: "Resolve ICurrentAccount in a hub's OnDisconnectedAsync"
layer: "infra"
deps: ["T19"]
blocks: []
acs: ["AC-08"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Infrastructure/SessionRevalidationHubFilter.cs", "tests/Rochambeau.UnitTests/Identity/SessionRevalidationHubFilterTests.cs", "tests/Rochambeau.IntegrationTests/Infrastructure/TestHubHost.cs", "tests/Rochambeau.IntegrationTests/Identity/HubSessionRevalidationTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14-2 R5"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-2.md` (R5); the source always wins. -->

# T34 — Resolve ICurrentAccount in a hub's OnDisconnectedAsync

## Why (review finding)

`IHubFilter.OnDisconnectedAsync` keeps its default pass-through, so `HubCallerPrincipal.User` is null there and `CurrentAccount` falls back to the untrustworthy `IHttpContextAccessor`. Play will need «who left» there (e.g. a forfeit).

— `_review/review-2026-11-14-2.md`, R5 · full text: [review](../_review/review-2026-11-14-2.md)

## Definition of Done

`OnDisconnectedAsync` sets the holder from the caller before `next`; unit test on the filter + integration test where the test hub's `OnDisconnectedAsync` records `ICurrentAccount.Current`. Unit + integration + format clean.
