---
id: T41
title: "Fix the stale HasBody comment and pin non-JSON body rejection on GET and DELETE"
layer: "ports"
deps: ["T40"]
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/JsonBodyMiddleware.cs", "tests/Rochambeau.UnitTests/Common/JsonBodyMiddlewareTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-15-3 R14, R15"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The findings are quoted from
`_review/review-2026-11-15-3.md` (R14, R15); the source always wins. -->

# T41 — Fix the stale HasBody comment and pin non-JSON body rejection on GET and DELETE

## Why (review findings)

R14: the `HasBody` doc comment still says a non-JSON Content-Type is rejected even with an empty body. Since T40 that holds only for POST / PUT / PATCH. R15: every rejection test uses POST. A later change that moved the whole condition behind `CarriesABody` would keep the suite green.

— `_review/review-2026-11-15-3.md`, R14 + R15 · full text: [review](../_review/review-2026-11-15-3.md)

## Definition of Done

The `HasBody` comment says the empty-body rejection applies to POST / PUT / PATCH. A unit theory (GET, DELETE) with `text/plain`, `contentLength: 5`, `canHaveBody: true` expects `next` not to be called and `400 common.invalid_request`. It pins existing behaviour, so it passes at once. Mutating the gate (wrapping the whole condition in `CarriesABody`) must make it fail. Unit + format clean.
