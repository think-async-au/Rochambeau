---
id: T14
title: "Measure the spec §6 latency targets for register, log in and view a profile"
layer: "tests"
deps: ["T8", "T9", "T11"]
blocks: []
acs: ["AC-01", "AC-04", "AC-11"]
files_hint: ["tests/Rochambeau.IntegrationTests/Identity/Performance/"]
owner: "a.udalov"
estimate: "S"
context_budget: "M"   # measured: 53 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T14 — Measure the spec §6 latency targets for register, log in and view a profile

## Place in the sequence

- **Blocked by:** T8 — Enforce the registration cap and prove the race guard on registration · T9 — Log in through POST /api/v1/session with lockout and enumeration-safe errors · T11 — Serve the public Profile through GET /api/v1/profiles/{username}
- **Blocks:** — (none)
- **Wave:** 7 — starts once T8, T9, T11 are done.
- **Lane:** own lane — new test files only.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

> **As a** Player
> **I want** to open another Player's public Profile by their Username
> **So that** I know who they are before I challenge them to a match
>
> — `spec.md §4, US-05, verbatim` · full text: [spec.md](../spec.md)

This task proves the three account flows stay inside the spec §6 latency budget.

## Inlined context

> **Latency p95 — register:** ≤ 500 ms (includes deliberately slow password protection) — measured as: p95 of 20 timed requests after 3 warm-up requests, in the integration test suite, local single instance
>
> **Latency p95 — log in:** ≤ 400 ms — measured as: p95 of 20 timed requests after 3 warm-up requests, in the integration test suite, local single instance
>
> **Latency p95 — view a profile:** ≤ 150 ms — measured as: p95 of 20 timed requests after 3 warm-up requests, in the integration test suite, local single instance
>
> — `spec.md §6, NFR latency rows, verbatim` · full text: [spec.md](../spec.md)

> **How verify:** latency — p95 of 20 timed requests after 3 warm-up requests, in the integration test suite, local single instance (spec §6), one test per endpoint. Session — integration test with a controlled clock: still logged in at 3.5 days − 1 minute, logged out at 7 days + 1 minute (spec §6).
>
> — `sad.md §10, QG-3 How verify, verbatim` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `POST /api/v1/accounts` → `201` · `POST /api/v1/session` → `200` · `GET /api/v1/profiles/{username}` → `200` (all as in T7, T9, T11).

— `contracts/openapi.yaml, operationIds registerAccount + logIn + getPublicProfile, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-01 — happy path

> **Given** a Visitor who is not logged in
> **When** the Visitor registers with a username, an email address and a password that are all valid and not yet taken
> **Then** the system creates the Account with the Player role, logs the Visitor in as that Player straight away, and shows their Profile
>
> — `spec.md §5, AC-01, verbatim` · full text: [spec.md](../spec.md)

### AC-04 — happy path

> **Given** a Visitor whose Account exists
> **When** the Visitor logs in with the correct email address (in any letter case, spaces at the edges ignored) and password
> **Then** the system logs them in as their Player and shows their Profile
>
> — `spec.md §5, AC-04, verbatim` · full text: [spec.md](../spec.md)

### AC-11 — happy path

> **Given** a logged-in Player and another Player with the username "Scissors"
> **When** the first Player opens the Profile of "Scissors" by its exact username (letter case ignored, e.g. "scissors")
> **Then** the system shows that Player's username, join date and role, but never their email address
>
> — `spec.md §5, AC-11, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] A helper that runs 3 warm-up requests, then 20 timed ones, and returns the p95 — `Identity/Performance/`
- [ ] Register: 23 unique usernames/emails, with a distinct test remote address per request (T8 mechanism) so the cap does not trigger. Assert p95 ≤ 500 ms — `Identity/Performance/`
- [ ] Log in: correct password every time (no lockout). Assert p95 ≤ 400 ms — `Identity/Performance/`
- [ ] View a profile: `GET /api/v1/profiles/{username}` as a logged-in Player. Assert p95 ≤ 150 ms — `Identity/Performance/`

## Edge cases

| Case | Behaviour |
|---|---|
| Registrations from one address | Would hit the cap after 5. Vary the test address |
| First request JIT / connection-pool warm-up | Excluded by the 3 warm-ups |
| Every response | Must be a success status; a failed request does not count as a timing |

## Definition of Done

- [ ] Three latency tests pass locally against Testcontainers PostgreSQL, using the spec §6 method exactly
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
