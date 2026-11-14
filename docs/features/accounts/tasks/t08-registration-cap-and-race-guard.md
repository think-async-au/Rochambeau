---
id: T8
title: "Enforce the registration cap and prove the race guard on registration"
layer: "app"
deps: ["T6", "T7"]
blocks: ["T14"]
acs: ["AC-03b", "AC-15"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/Register/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Infrastructure/", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 43 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T8 — Enforce the registration cap and prove the race guard on registration

## Place in the sequence

- **Blocked by:** T6 — Implement the in-process RegistrationCap sliding window · T7 — Register an Account through POST /api/v1/accounts
- **Blocks:** T14 — Measure the spec §6 latency targets for register, log in and view a profile
- **Wave:** 6 — starts once T6, T7 are done.
- **Lane:** shares `Application/Register/` with T7 — serialized after it.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task makes registration safe under load: at most 5 per address per hour, and exactly one winner when two people race for the same name or email.

## Inlined context

> Order inside register: field rules first (a validation failure reserves nothing) → reserve a cap slot (none free → cap message) → insert → unique violation: release the slot → inserted: confirm the slot.
>
> — `sad.md §6, critical flow 1, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** The unique constraints on the normalised Username and email are the only race guard (AC-03b). The application never does a «check, then insert». When two parallel inserts collide, the database rejects the second one, and the Visitor who sent it gets the same «taken» answer as in AC-03.
>
> — `sad.md §6, critical flow 1 note, verbatim` · full text: [sad.md](../sad.md)

> The address is the connection's remote address until the spec §8 proxy question is answered.
>
> — `sad.md §8, Registration cap row, abridged` · full text: [sad.md](../sad.md)

> **How verify:** integration test firing parallel registrations (spec §6 «Concurrency»): N parallel requests for the same normalised username and, separately, the same email, then assert exactly one row and N−1 «taken» answers. It runs on every CI build (spec §7 «Duplicate usernames»: target 0).
>
> — `sad.md §10, QG-2 How verify, verbatim` · full text: [sad.md](../sad.md)

> **Hard rule:** Structured log events through `ILogger`, keyed by account id and never containing the email or password: account registered, login failed, account locked out, registration cap hit, role changed, hub connection aborted after revocation.
>
> — `sad.md §7, Monitoring, verbatim` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `POST /api/v1/accounts` → `429` `identity.registration_cap`, title "Too many accounts were created from your connection recently. Try again later." Failed attempts do not count.
- Losing side of a concurrent registration → the same `409` `identity.username_taken` / `identity.email_unavailable` as AC-03.

— `contracts/openapi.yaml, operationId registerAccount 409 + 429, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-03b — domain invariant, concurrent

> **Given** two Visitors who both choose the username "Paper" (in any letter case, e.g. "Paper" and "paper") or the same email address at the same moment, and no Account holds it yet
> **When** both submit their registration
> **Then** exactly one Account is created with that username (or email address), and the other Visitor is told the username is already taken (or the email address cannot be used)
>
> — `spec.md §5, AC-03b, verbatim` · full text: [spec.md](../spec.md)

### AC-15 — authorization

> **Given** 5 Accounts have already been registered successfully from the same network address within the last 60 minutes (failed attempts do not count)
> **When** a Visitor from that address tries to register another Account
> **Then** the system creates no Account and tells the Visitor that too many accounts were created from their connection recently and to try again later
>
> — `spec.md §5, AC-15, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] Register `RegistrationCap` as a singleton — `IdentityModule.cs`
- [ ] Handler: after validation, `TryReserve(remote address)` → capped → `Error` too-many-requests `identity.registration_cap`. Use try/finally: confirm on success, release on **any** later failure (409, Identity error, exception). Log "registration cap hit" without the address — `Application/Register/`
- [ ] Address key = `HttpContext.Connection.RemoteIpAddress`. **Breakdown default, not upstream:** when it is null (as under TestServer), use one shared "unknown" bucket, so the cap is never silently skipped — `Application/Register/`
- [ ] Test host: a way to set the remote address per request (e.g. a test-only middleware), so cap tests and parallel tests don't share one bucket; plus the fake `TimeProvider` — `tests/Rochambeau.IntegrationTests/Infrastructure/`
- [ ] Integration tests: AC-15 (5 ok → 6th `429` → after 60 min ok; failed attempts don't count); AC-03b (N parallel same normalised username, then same email → exactly 1 row and N−1 `409`); QG-1 (h) — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Registration fails validation (`400`) | No slot reserved |
| Registration loses the race (`409`) | Slot released; it does not count toward the cap |
| 10 parallel from an address with 4 in the window | Exactly one more Account |
| Race tests with N > 5 from one address | Would hit the cap. Give each request its own test address |

## Definition of Done

- [ ] AC-03b concurrency tests (username and email) pass on every run: exactly one row, N−1 `409`
- [ ] AC-15 tests pass with a fake clock, including "failed attempts don't count" and QG-1 (h)
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
