---
id: T23
title: "Give request-binding failures a ProblemDetails errorCode"
layer: "ports"
deps: ["T22"]
blocks: []
acs: ["AC-02"]
files_hint: ["src/Rochambeau.Api/Common/Http/", "src/Rochambeau.Api/Program.cs", "docs/features/accounts/contracts/openapi.yaml", "tests/Rochambeau.IntegrationTests/Identity/RegisterTests.cs", "tests/Rochambeau.IntegrationTests/Identity/LogInTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F8"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F8); the source always wins. -->

# T23 — Give request-binding failures a ProblemDetails errorCode

## Why (review finding)

A malformed body (`{bad`, an empty body, a number where a string is expected) on `POST /api/v1/accounts` or `POST /api/v1/session` fails in parameter binding and returns the framework's 400 without `errorCode`, while `contracts/openapi.yaml` `ProblemDetails.required` lists `errorCode`.

— `_review/review-2026-11-14.md`, F8 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Integration tests: a malformed and an empty body on both endpoints return 400 ProblemDetails carrying a contract-listed `errorCode`. Unit + integration + format clean.