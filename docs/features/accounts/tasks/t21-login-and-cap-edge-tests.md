---
id: T21
title: "Add the mandatory login and registration-cap edge-case tests"
layer: "tests"
deps: []
blocks: []
acs: ["AC-05", "AC-06", "AC-15"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/LogInTests.cs", "tests/Rochambeau.IntegrationTests/Identity/RegistrationCapAndRaceTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F5"
status: "todo"
step: "—"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F5); the source always wins. -->

# T21 — Add the mandatory login and registration-cap edge-case tests

## Why (review finding)

test-plan.md:102,103,107 mark as mandatory: (a) a log-in with an unknown email leaves no lockout state, (b) a lockout of Account A does not block Account B, (c) registration fails closed when the database is unreachable and no cap slot is consumed. The code appears to handle all three; the tests are missing.

— `_review/review-2026-11-14.md`, F5 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Three integration tests asserting (a), (b), (c). Production code is expected to already satisfy them, so a first-run green is checked by mutation (temporarily breaking the guarded behaviour turns each red). Integration + format clean.