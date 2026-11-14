---
id: T22
title: "Make the lockout check and failure count atomic under concurrent log-ins"
layer: "app"
deps: ["T21"]
blocks: []
acs: ["AC-06"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/LogIn/LogIn.cs", "tests/Rochambeau.IntegrationTests/Identity/LogInTests.cs"]
owner: "a.udalov"
estimate: "M"
origin: "review-2026-11-14 F7"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F7); the source always wins. -->

# T22 — Make the lockout check and failure count atomic under concurrent log-ins

## Why (review finding)

`LogIn.cs` reads `LockoutEnd` before the slow hash check; neither the failure `UPDATE … RETURNING` nor the success path re-checks it, so N parallel attempts get ~N guesses per window and an in-flight correct guess signs in on a just-locked account. AC-06: 5 failures in a row block the account for 15 minutes.

— `_review/review-2026-11-14.md`, F7 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

Integration test firing ~20 wrong passwords in parallel: at most 5 are evaluated as failures before the block (the rest answer 423), and a correct password arriving while locked gets no session. Failure write and success reset are conditional on `lockout_end IS NULL OR lockout_end <= now`; zero rows → 423 from the stored end. Unit + integration + format clean.