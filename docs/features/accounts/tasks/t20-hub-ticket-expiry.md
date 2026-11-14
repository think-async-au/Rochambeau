---
id: T20
title: "Reject hub invocations once the session ticket has expired"
layer: "infra"
deps: ["T19"]
blocks: []
acs: ["AC-08"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Infrastructure/SessionRevalidationHubFilter.cs", "tests/Rochambeau.IntegrationTests/Identity/HubSessionRevalidationTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F2"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F2); the source always wins. -->

# T20 — Reject hub invocations once the session ticket has expired

## Why (review finding)

`SessionRevalidationHubFilter.cs:49-63` compares only the security stamp; the auth ticket's `ExpiresUtc` is never checked, so a connection open longer than 7 days without activity still acts as the Player. AC-08: "nobody" after 7 days without activity.

— `_review/review-2026-11-14.md`, F2 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Fake-clock integration test: an invocation 7 days + 1 minute after the last activity is refused as not authenticated; one before expiry still succeeds. Unit + integration + format clean.