---
id: T38
title: "Prove ICurrentAccount over HTTP and refuse a Visitor an unknown Profile"
layer: "tests"
deps: ["T5", "T11", "T12"]
blocks: []
acs: ["AC-08", "AC-13", "AC-10"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/SessionWiringTests.cs", "tests/Rochambeau.IntegrationTests/Identity/PublicProfileTests.cs", "tests/Rochambeau.IntegrationTests/Identity/IdentityApi.cs"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-15 R10"
status: "todo"
step: "SELECT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-15.md` (R10); the source always wins. -->

# T38 — Prove ICurrentAccount over HTTP and refuse a Visitor an unknown Profile

## Why (review finding)

`test-plan.md:59` promises a test endpoint in the test host that gets the Player's account identity, username and role over HTTP, and no such integration test exists: `/me` reads the role from the database, and the HTTP principal-to-role mapping is unit-tested only. `test-plan.md:69` says "whether or not the username exists", but `PublicProfileTests.cs:70` tries only an existing username.

— `_review/review-2026-11-15.md`, R10 · full text: [review](../_review/review-2026-11-15.md)

## Definition of Done

`SessionWiringTests.ProbeHost` gets a `/__probe/whoami` endpoint that returns `ICurrentAccount.Current`. Integration tests: a logged-in Player gets their account id, username and `Player`; an Account promoted through the operator role-change path, after logging in again, gets `Administrator`; a Visitor gets nobody. A Visitor looking up an unknown username gets `401 identity.not_authenticated` with no profile fields. Unit + format clean; integration NON-red if Docker is absent.
