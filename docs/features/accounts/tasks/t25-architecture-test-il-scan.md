---
id: T25
title: "Make the module-boundary architecture test see method-body references"
layer: "tests"
deps: []
blocks: []
acs: ["AC-08"]
files_hint: ["tests/Rochambeau.UnitTests/Identity/ArchitectureTests.cs", "tests/Rochambeau.UnitTests/Rochambeau.UnitTests.csproj"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F11"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F11); the source always wins. -->

# T25 — Make the module-boundary architecture test see method-body references

## Why (review finding)

`ArchitectureTests.cs:52-65` reads only signatures, fields, properties and local-variable types; IL member/type references (calls, generic method arguments, static member access, async state-machine code) are missed, so e.g. `await db.Set<Account>().CountAsync()` in another module would pass (sad §10 QG-4, accounts ADR-0004).

— `_review/review-2026-11-14.md`, F11 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Unit test: an offender that only calls `db.Set<Account>()` inside an async method, and one that only reads a static Identity-internal member in a method body, are both detected; the real assembly still passes. Unit + format clean.