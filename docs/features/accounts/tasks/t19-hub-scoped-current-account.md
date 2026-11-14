---
id: T19
title: "Resolve ICurrentAccount from the hub caller inside hub invocations"
layer: "infra"
deps: ["T13"]
blocks: []
acs: ["AC-08"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Infrastructure/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Infrastructure/TestHubHost.cs", "tests/Rochambeau.IntegrationTests/Identity/HubSessionRevalidationTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F1"
status: "todo"
step: "—"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F1); the source always wins. -->

# T19 — Resolve ICurrentAccount from the hub caller inside hub invocations

## Why (review finding)

`CurrentAccount.cs:14` reads `IHttpContextAccessor.HttpContext`; on long-polling/SSE the request that started the connection has ended, so a hub would see "nobody" for a logged-in Player. The test hub never resolves `ICurrentAccount` (test-plan.md:60,63). AC-08: Accounts answers with the account identity, username and role for the current HTTP request **or hub invocation** (accounts ADR-0004).

— `_review/review-2026-11-14.md`, F1 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Integration test: a test-hub method returning `ICurrentAccount.Current` yields the logged-in Player's id, username and role over **WebSockets and LongPolling**. `SessionRevalidationHubFilter` sets a scoped holder from `HubCallerContext.User` that `CurrentAccount` reads before the accessor. Unit + integration + `dotnet format --verify-no-changes` clean.