---
id: T18
title: "Build the own Profile and public Profile screens"
layer: "ui"
deps: ["T17"]
blocks: []
acs: ["AC-09", "AC-10", "AC-11", "AC-12", "AC-13"]
files_hint: ["web/src/features/profile/", "web/src/App.tsx"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 60 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T18 — Build the own Profile and public Profile screens

## Place in the sequence

- **Blocked by:** T17 — Build the Log-in screen, the log-out action and the logged-in route guard
- **Blocks:** — (none)
- **Wave:** 4 — starts once T17 is done.
- **Lane:** shares `web/src/App.tsx` with T16/T17 — serialized.

## Why (user story)

> **As a** Player
> **I want** to see my Profile, including my private email address
> **So that** I can check how my account looks and which details the portal holds about me
>
> — `spec.md §4, US-04, verbatim` · full text: [spec.md](../spec.md)

> **As a** Player
> **I want** to open another Player's public Profile by their Username
> **So that** I know who they are before I challenge them to a match
>
> — `spec.md §4, US-05, verbatim` · full text: [spec.md](../spec.md)

This task shows the Player their own Profile, including the email and the Administrator role when they have it. It also lets them open another Player's public Profile by Username.

## Inlined context

> **Hard rule:** **Personal data:** The email appears only in the owner's own-profile response. The public-profile response type and `AccountIdentity` have no email field at all (AC-09, AC-11). All text is rendered as plain text by React, never as HTML
>
> — `sad.md §8, Personal data row, verbatim` · full text: [sad.md](../sad.md)

> Own Profile: "shows own Profile, role shown as Player or Administrator". Public: found → "shows the public Profile"; none → "says no player with that username exists"; nobody → "asks to log in or register first".
>
> — `sad.md §6, See my own Profile + Look up another Player's Profile (UI lines), abridged` · full text: [sad.md](../sad.md)

> Reuse `TextField`, `Button`, `FormError` (T16), `RequireAuth` (T17), and `apiFetch` / `useMe` (T15). Routes `/profile` and `/players/:username`, both guarded.
>
> — `T16 screen contract (breakdown default), abridged` · full text: [t16-web-register-screen.md](./t16-web-register-screen.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `GET /api/v1/me` → `OwnProfile` {`username`, `email`, `joinedAt`, `role`}.
- `GET /api/v1/profiles/{username}` → `200` `PublicProfile` {`username`, `joinedAt`, `role`} · `404` `identity.player_not_found` "No player with that username exists." · `401`.

— `contracts/openapi.yaml, operationIds getOwnProfile + getPublicProfile, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-09 — happy path

> **Given** a logged-in Player
> **When** the Player opens their own Profile
> **Then** the system shows their username, email address, join date and role
>
> — `spec.md §5, AC-09, verbatim` · full text: [spec.md](../spec.md)

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

### AC-13 — happy path

> **Given** an Account that the portal's operator has designated as an administrator
> **When** its owner logs in
> **Then** the system recognises them as an Administrator and their Profile shows the Administrator role
>
> — `spec.md §5, AC-13, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `OwnProfilePage` from `useMe`: username, email, join date, and a role label "Player" / "Administrator"; plus a "look up a player" field that navigates to `/players/<encoded username>` and offers no suggestions — `web/src/features/profile/`
- [ ] `PublicProfilePage`: `usePublicProfile(username)` → username (as returned), join date and role, with no email element; on `404`, the server message — `web/src/features/profile/`
- [ ] Routes `/profile` and `/players/:username` inside `RequireAuth`, plus a log-out button on the profile — `web/src/App.tsx`
- [ ] RTL tests — `web/src/features/profile/*.test.tsx`

## Edge cases

| Case | Behaviour |
|---|---|
| Own Profile of an Administrator | Role label "Administrator" (AC-13) |
| Public Profile | No email rendered, even if a field were present |
| `/players/sciss` | "No player with that username exists." |
| Username with characters needing URL encoding | Encoded with `encodeURIComponent` |

## Definition of Done

- [ ] RTL tests: own profile shows the email + role, public profile shows no email, the `404` message, and the Administrator label
- [ ] `npm --prefix web test`, `lint` and `build` clean
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
