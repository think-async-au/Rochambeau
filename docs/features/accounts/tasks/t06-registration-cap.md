---
id: T6
title: "Implement the in-process RegistrationCap sliding window"
layer: "app"
deps: ["T2"]
blocks: ["T8"]
acs: ["AC-15"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/RegistrationCap/", "tests/Rochambeau.UnitTests/Identity/"]
owner: "a.udalov"
estimate: "S"
context_budget: "S"   # measured: 35 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T6 — Implement the in-process RegistrationCap sliding window

## Place in the sequence

- **Blocked by:** T2 — Encode the Identity domain rules: username, email, password, role, lockout and cap policy
- **Blocks:** T8 — Enforce the registration cap and prove the race guard on registration
- **Wave:** 2 — starts once T2 is done.
- **Lane:** own lane — pure application logic with a controlled clock; runs in parallel with T3–T5. DI registration is done in T8.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task builds the counter that stops one connection from mass-creating Accounts (AC-15), without ever storing the network address with an Account.

## Inlined context

> **Hard rule:** **Registration cap:** An in-process sliding-window counter of **successful** registrations per network address: 5 per 60 minutes (AC-15). The check and the reservation are one atomic step under a per-address lock, done **before** the insert. A failed registration releases its slot, so parallel requests cannot exceed the cap and failed attempts still don't count. It reads time from `TimeProvider` and keeps addresses only in memory, never with the Account (spec §6.1). The address is the connection's remote address until the spec §8 proxy question is answered
>
> — `sad.md §8, Registration cap row, verbatim` · full text: [sad.md](../sad.md)

> Flow: all fields valid → reserve a cap slot atomically → insert. Unique violation → release the reserved slot. Inserted → confirm the slot. No free slot (5 in 60 minutes) → "too many accounts from this connection, try later".
>
> — `sad.md §6, critical flow 1, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** The network address used for the registration cap (AC-15) is held only for the rolling 60 minutes and is not stored with the Account.
>
> — `spec.md §6.1, Personal data touched, abridged` · full text: [spec.md](../spec.md)

> **Risk:** The registration-cap counter is in memory: it resets on restart and counts per instance — **Mitigation:** Acceptable for one instance. Move it to a shared store under an ADR before a second instance runs (§7)
>
> — `sad.md §11, in-memory counter row, verbatim` · full text: [sad.md](../sad.md)

> QG-1 (h): 10 parallel registrations arrive from one network address that already has 4 in the last 60 minutes → exactly one more Account is created.
>
> — `sad.md §10, QG-1 When/Then (h), abridged` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

Internal — no API surface.

## Acceptance criteria

### AC-15 — authorization

> **Given** 5 Accounts have already been registered successfully from the same network address within the last 60 minutes (failed attempts do not count)
> **When** a Visitor from that address tries to register another Account
> **Then** the system creates no Account and tells the Visitor that too many accounts were created from their connection recently and to try again later
>
> — `spec.md §5, AC-15, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `RegistrationCap` (singleton-safe) with `TryReserve(addressKey)` → a reservation or "capped"; `Confirm(reservation)`; `Release(reservation)` — `Application/RegistrationCap/`
- [ ] One lock per address. Count confirmed timestamps newer than `now − 60 min` **plus** pending reservations against `RegistrationCapPolicy` (T2). Read time only from `TimeProvider` — `Application/RegistrationCap/`
- [ ] Prune expired timestamps, and drop empty address entries, so memory does not grow without bound — `Application/RegistrationCap/`
- [ ] Unit tests with a fake `TimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) for every edge below — `tests/Rochambeau.UnitTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| 5 confirmed in the window, 6th `TryReserve` | "capped" |
| Window slides: oldest confirmation becomes 60 min old | A slot frees up |
| Reservation released (registration failed) | Does not count — failed attempts never count (AC-15) |
| 4 confirmed + 10 parallel `TryReserve` | Exactly 1 reservation granted (pending reservations count) |
| Two different addresses | Independent windows |

## Definition of Done

- [ ] Unit tests for every edge case pass, including the 10-way parallel case
- [ ] No address is logged or persisted; the cap state lives only in memory
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
