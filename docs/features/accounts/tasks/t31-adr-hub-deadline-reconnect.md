---
id: T31
title: "Record the per-connection hub deadline in ADR-0004 and require automatic reconnect"
layer: "docs"
deps: ["T20"]
blocks: []
acs: ["AC-08"]
files_hint: ["docs/features/accounts/adr/0004-expose-acting-account-through-identity-contracts.md", "CLAUDE.md"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14-2 R2"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-2.md` (R2); the source always wins. -->

# T31 — Record the per-connection hub deadline in ADR-0004 and require automatic reconnect

## Why (review finding)

A hub connection's 7-day deadline is set at connect and moves only on hub invocations (`SessionRevalidationHubFilter.cs:34,42-45,65`), not on the Player's HTTP activity. The owner accepted the per-connection deadline as a deviation, on condition that clients reconnect.

— `_review/review-2026-11-14-2.md`, R2 · full text: [review](../_review/review-2026-11-14-2.md)

## Definition of Done

ADR-0004 Consequences records the deviation; every `web/src/shared/realtime` hook must use `withAutomaticReconnect` so a refused call reconnects with the still-valid cookie (which renews the deadline); CLAUDE.md · Real-time carries the rule.
