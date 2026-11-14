---
id: T30
title: "Pin the in-flight lockout branches of log in with deterministic tests"
layer: "tests"
deps: ["T22"]
blocks: []
acs: ["AC-06"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/LogInTests.cs"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14-2 R1"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14-2.md` (R1); the source always wins. -->

# T30 — Pin the in-flight lockout branches of log in with deterministic tests

## Why (review finding)

F7 made the lockout writes atomic, but no test reaches the success-path refusal (`LogIn.cs:80-83`) or the failure path's zero-rows branch (`:115-118`). The parallel test sends only wrong passwords and its counts usually match the pre-fix code too. AC-06: 5 failures in a row block the account for 15 minutes.

— `_review/review-2026-11-14-2.md`, R1 · full text: [review](../_review/review-2026-11-14-2.md)

## Definition of Done

Integration tests with an `IPasswordHasher<Account>` decorator that writes `lockout_end = now + 15 min` through a separate DbContext before delegating: a correct password gets 423 with the stored `lockoutEnd`, no `Set-Cookie`, unchanged `access_failed_count`; a wrong password gets 423 with the stored end and counts nothing. Unit + integration + format clean.
