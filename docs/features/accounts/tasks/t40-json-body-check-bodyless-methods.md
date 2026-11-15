---
id: T40
title: "Limit the empty-body Content-Type check to methods that carry a body"
layer: "ports"
deps: ["T37"]
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/JsonBodyMiddleware.cs", "tests/Rochambeau.UnitTests/Common/JsonBodyMiddlewareTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-15-2 R13"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-15-2.md` (R13); the source always wins. -->

# T40 — Limit the empty-body Content-Type check to methods that carry a body

## Why (review finding)

Since T37, `|| request.ContentType is not null` also catches a bodyless `DELETE /api/v1/session` or `GET /api/v1/me` / `/profiles/{username}` that carries a non-JSON Content-Type. Such a request now gets `400 common.invalid_request`, which those operations do not document.

— `_review/review-2026-11-15-2.md`, R13 · full text: [review](../_review/review-2026-11-15-2.md)

## Definition of Done

The empty-body Content-Type check applies only to `POST` / `PUT` / `PATCH`. Unit tests: `DELETE` and `GET` with `text/plain`, CL 0, `canHaveBody: false` → `next` is called; the existing empty-body `text/plain` `POST` → 400 still holds. Unit + format clean.
