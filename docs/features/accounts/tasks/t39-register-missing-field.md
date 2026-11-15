---
id: T39
title: "Prove registration with a missing field reports that field"
layer: "tests"
deps: ["T7"]
blocks: []
acs: ["AC-02"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/RegisterTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-15 R11"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-15.md` (R11); the source always wins. -->

# T39 — Prove registration with a missing field reports that field

## Why (review finding)

`RegisterAsync` replaces every `null` with a default value, so no registration test ever omits a JSON property. Read in the code, the production path names the missing field, but no test proves the `test-plan.md:101` row end to end.

— `_review/review-2026-11-15.md`, R11 · full text: [review](../_review/review-2026-11-15.md)

## Definition of Done

Integration test: a raw JSON body without `email` posted to `/api/v1/accounts` gets `400 identity.validation_failed` with an `errors` object keyed exactly by `email`, and no Account is stored. Unit + format clean; integration NON-red if Docker is absent.
