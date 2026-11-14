---
id: T33
title: "Bring 403 and 415 error responses inside the contract"
layer: "ports"
deps: ["T23"]
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/ProblemDetailsDefaults.cs", "docs/features/accounts/contracts/openapi.yaml", "tests/Rochambeau.UnitTests/Common/ProblemDetailsDefaultsTests.cs", "tests/Rochambeau.IntegrationTests/Host/BadRequestBodyTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14-2 R4"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-2.md` (R4); the source always wins. -->

# T33 — Bring 403 and 415 error responses inside the contract

## Why (review finding)

`identity.forbidden` (403) is emitted (`IdentityErrors.cs:11-12`, `SessionCookie.cs:33-34`) but missing from the errorCode enum; a non-JSON `Content-Type` on POST `/api/v1/accounts` or `/api/v1/session` answers an undocumented 415 with no `errorCode` (`ProblemDetailsDefaults.cs:19-20`).

— `_review/review-2026-11-14-2.md`, R4 · full text: [review](../_review/review-2026-11-14-2.md)

## Definition of Done

openapi.yaml enum lists `identity.forbidden` with a Forbidden response component; a non-JSON Content-Type answers 400 `common.invalid_request` (unit test on the ProblemDetails customization + integration case in BadRequestBodyTests). Unit + integration + format clean.
