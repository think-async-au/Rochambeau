---
id: T24
title: "Apply the client-header check to negotiate with a trailing slash"
layer: "ports"
deps: []
blocks: []
acs: []
files_hint: ["src/Rochambeau.Api/Common/Http/ClientHeaderMiddleware.cs", "tests/Rochambeau.IntegrationTests/Host/ClientHeaderTests.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14 F10"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F10); the source always wins. -->

# T24 — Apply the client-header check to negotiate with a trailing slash

## Why (review finding)

`ClientHeaderMiddleware.cs:42-43` matches `Path.Value.EndsWith("/negotiate")`; endpoint routing ignores a trailing slash, so `POST /hubs/<hub>/negotiate/` reaches SignalR without the `X-Rochambeau-Client` check (accounts ADR-0005).

— `_review/review-2026-11-14.md`, F10 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Integration test: `POST …/negotiate/` without the header is rejected like the slash-less path. Integration + format clean.