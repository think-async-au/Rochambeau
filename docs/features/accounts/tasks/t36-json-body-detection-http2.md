---
id: T36
title: "Detect a request body on HTTP/2 and HTTP/3 in the JSON-body check"
layer: "ports"
deps: ["T33"]
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/JsonBodyMiddleware.cs", "tests/Rochambeau.UnitTests/Common/JsonBodyMiddlewareTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14-3 R8"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-3.md` (R8); the source always wins. -->

# T36 — Detect a request body on HTTP/2 and HTTP/3 in the JSON-body check

## Why (review finding)

`HasBody` tests only `ContentLength > 0` or `Transfer-Encoding`. HTTP/2 and HTTP/3 have no Transfer-Encoding, so a streamed non-JSON body passes and gets a 415 or 401 with no `errorCode`.

— `_review/review-2026-11-14-3.md`, R8 · full text: [review](../_review/review-2026-11-14-3.md)

## Definition of Done

`JsonBodyMiddleware` treats a request as having a body when `IHttpRequestBodyDetectionFeature.CanHaveBody` is true, keeping the length / Transfer-Encoding check as a fallback. A unit test with a body-detection feature and no `Content-Length` gets `400 common.invalid_request`. A bodiless request still passes. Unit + format clean.
