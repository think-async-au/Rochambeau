---
id: T35
title: "Prove ICurrentAccount carries the Administrator role on a hub connection"
layer: "tests"
deps: ["T12", "T19"]
blocks: []
acs: ["AC-13", "AC-08"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/HubSessionRevalidationTests.cs", "tests/Rochambeau.IntegrationTests/Infrastructure/TestHubHost.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14-3 R6"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-3.md` (R6); the source always wins. -->

# T35 — Prove ICurrentAccount carries the Administrator role on a hub connection

## Why (review finding)

AC-13's "Administrator in the acting identity" is asserted only through `/me`, which reads the role from the database, not from `ICurrentAccount`. The hub `WhoAmI` test uses only a Player.

— `_review/review-2026-11-14-3.md`, R6 · full text: [review](../_review/review-2026-11-14-3.md)

## Definition of Done

Integration test: register, promote through the operator role-change path, log in again, connect to the test hub, and `WhoAmI` returns the username and `AccountRole.Administrator`. Unit + integration + format clean.
