---
id: T11
title: "Serve the public Profile through GET /api/v1/profiles/{username}"
layer: "ports"
deps: ["T7"]
blocks: ["T14"]
acs: ["AC-10", "AC-11", "AC-12"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/Profiles/", "src/Rochambeau.Api/Modules/Identity/Endpoints/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "S"
context_budget: "M"   # measured: 44 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T11 — Serve the public Profile through GET /api/v1/profiles/{username}

## Place in the sequence

- **Blocked by:** T7 — Register an Account through POST /api/v1/accounts
- **Blocks:** T14 — Measure the spec §6 latency targets for register, log in and view a profile
- **Wave:** 6 — starts once T7 is done.
- **Lane:** shares `Application/Profiles/` with T10, and `Endpoints/` with T7–T10 — serialized; it can run before or after T9/T10.

## Why (user story)

> **As a** Player
> **I want** to open another Player's public Profile by their Username
> **So that** I know who they are before I challenge them to a match
>
> — `spec.md §4, US-05, verbatim` · full text: [spec.md](../spec.md)

This task lets a Player look up a future opponent by exact Username without ever seeing their email.

## Inlined context

> Public profile: resolve who is acting → nobody: "not logged in, no profile details" → a Player: trim and normalise the username → look up by normalised username, exact match only → none: "no player with that username" → found: username as the owner typed it, join date and role. The response has no email field.
>
> — `sad.md §6, Look up another Player's Profile, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** **Personal data:** The email appears only in the owner's own-profile response. The public-profile response type and `AccountIdentity` have no email field at all (AC-09, AC-11). All text is rendered as plain text by React, never as HTML
>
> — `sad.md §8, Personal data row, verbatim` · full text: [sad.md](../sad.md)

> QG-1 (d) asserts the JSON has no `email` property. (g) requests the own and the public Profile anonymously and asserts 401 with no profile fields. Together, (d) and (g) are the spec §7 «Email exposure» check, run on every CI build.
>
> — `sad.md §10, QG-1 How verify, abridged` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `GET /api/v1/profiles/{username}` (`getPublicProfile`); path `username`, any letter case.
- `200` `PublicProfile` {`username`, `joinedAt`, `role`} — `additionalProperties: false`, no email field at all.
- `401` `identity.not_authenticated` · `404` `identity.player_not_found`, title "No player with that username exists." Any value no Account holds, including one that could never be a valid Username, is `404`.

— `contracts/openapi.yaml, operationId getPublicProfile, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-10 — authorization

> **Given** a Visitor who is not logged in
> **When** the Visitor tries to open any Profile, their own or someone else's
> **Then** the system shows no profile details and asks them to log in or register first
>
> — `spec.md §5, AC-10, verbatim` · full text: [spec.md](../spec.md)

### AC-11 — happy path

> **Given** a logged-in Player and another Player with the username "Scissors"
> **When** the first Player opens the Profile of "Scissors" by its exact username (letter case ignored, e.g. "scissors")
> **Then** the system shows that Player's username, join date and role, but never their email address
>
> — `spec.md §5, AC-11, verbatim` · full text: [spec.md](../spec.md)

### AC-12 — error

> **Given** a logged-in Player
> **When** the Player looks up a username that no Account holds (no partial matches are offered)
> **Then** the system tells them that no player with that username exists
>
> — `spec.md §5, AC-12, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] GetPublicProfile handler: trim → normalise → exact lookup on `normalized_user_name` → a `PublicProfile` type that has no email member — `Application/Profiles/`
- [ ] Endpoint `MapGet("/api/v1/profiles/{username}")` — `Endpoints/`, `IdentityModule.cs`
- [ ] Integration tests for AC-10, AC-11 (assert on the raw JSON that `email` is absent) and AC-12 — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| `scissors` for `Scissors` | `200`, `username: "Scissors"` |
| `sciss` | `404` — no partial match |
| `a` or `x!y` (could never be valid) | `404`, not `400` |
| Anonymous | `401`, no profile fields |

## Definition of Done

- [ ] Integration tests for AC-10, AC-11 and AC-12 pass
- [ ] The `200` JSON has exactly `username`, `joinedAt`, `role`
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
