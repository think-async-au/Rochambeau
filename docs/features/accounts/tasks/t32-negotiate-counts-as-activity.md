---
id: T32
title: "Prove that opening a real-time connection counts as activity"
layer: "tests"
deps: ["T20"]
blocks: []
acs: ["AC-08"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/HubSessionRevalidationTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14-2 R3"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-2.md` (R3); the source always wins. -->

# T32 — Prove that opening a real-time connection counts as activity

## Why (review finding)

AC-08's clause «opening a real-time connection counts as activity» (ADR-0004:40) has no test: the hub helper reuses the cookie captured at registration, so nothing asserts the renewal negotiate performs.

— `_review/review-2026-11-14-2.md`, R3 · full text: [review](../_review/review-2026-11-14-2.md)

## Definition of Done

Fake-clock integration test: register at t0; at +6 days POST the test hub's negotiate with the cookie and assert a renewed session `Set-Cookie`; at a further +6 days `GET /api/v1/me` with the renewed cookie answers 200. Unit + integration + format clean.
