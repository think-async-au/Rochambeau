---
id: T37
title: "Close the empty-body edge cases of the JSON-body check"
layer: "ports"
deps: ["T36"]
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/JsonBodyMiddleware.cs", "tests/Rochambeau.UnitTests/Common/JsonBodyMiddlewareTests.cs", "tests/Rochambeau.IntegrationTests/Host/BadRequestBodyTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-15 R9"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-15.md` (R9); the source always wins. -->

# T37 — Close the empty-body edge cases of the JSON-body check

## Why (review finding)

(a) Since T36, on HTTP/2 Kestrel derives `CanHaveBody` from END_STREAM, not from the declared length. A bodyless request that sends `content-length: 0` and then an empty DATA frame has `CanHaveBody == true`, so a log-out `DELETE` with no Content-Type gets `400 common.invalid_request`. (b) A request with a non-JSON Content-Type and an empty body passes the check and reaches the framework's 415 endpoint, which answers 401 for a Visitor or a 415 with no `errorCode`.

— `_review/review-2026-11-15.md`, R9 · full text: [review](../_review/review-2026-11-15.md)

## Definition of Done

Under `/api`, an explicit `Content-Length: 0` counts as no body, and a request that has a body **or** carries a Content-Type is rejected with `400 common.invalid_request` when that Content-Type is not JSON. Unit cases: `DELETE` with no Content-Type, `contentLength: 0`, `canHaveBody: true` → `next` is called; empty-body `text/plain` → 400. Integration case: empty-body `text/plain` POST to `/api/v1/accounts` → 400 `common.invalid_request`. Unit + format clean.
