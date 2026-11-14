---
id: T17
title: "Build the Log-in screen, the log-out action and the logged-in route guard"
layer: "ui"
deps: ["T16"]
blocks: ["T18"]
acs: ["AC-04", "AC-05", "AC-06", "AC-07", "AC-10"]
files_hint: ["web/src/features/auth/", "web/src/App.tsx"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 60 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T17 — Build the Log-in screen, the log-out action and the logged-in route guard

## Place in the sequence

- **Blocked by:** T16 — Build the Register screen with field-level errors
- **Blocks:** T18 — Build the own Profile and public Profile screens
- **Wave:** 3 — starts once T16 is done.
- **Lane:** shares `web/src/features/auth/` + `web/src/App.tsx` with T16/T18 — serialized.

## Why (user story)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

> **As a** Player
> **I want** to log out
> **So that** nobody else using this browser can act as me
>
> — `spec.md §4, US-03, verbatim` · full text: [spec.md](../spec.md)

This task lets a returning Player log in and out, and keeps Visitors away from logged-in pages.

## Inlined context

> Login UI branches: incorrect → "shows the shared message, stays on the form". Blocked → "shows the block message and when to retry". Success → "shows own Profile, with the Administrator role if the Account has it".
>
> — `sad.md §6, Log in, with lockout (UI lines), abridged` · full text: [sad.md](../sad.md)

> Logout UI: log out with this browser's session → forget the cached own identity → show the portal as a Visitor sees it → the Profile page asks to log in or register.
>
> — `sad.md §6, Log out, abridged` · full text: [sad.md](../sad.md)

> Reuse `TextField`, `Button`, `FormError` from `web/src/shared/ui/` (T16) and `apiFetch` / `useMe` from `web/src/shared/api/` (T15). Routes: `/login`; guarded `/profile` and `/players/:username`.
>
> — `T16 screen contract (breakdown default), abridged` · full text: [t16-web-register-screen.md](./t16-web-register-screen.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `POST /api/v1/session` {`email`, `password`} → `200` `OwnProfile` · `401` `identity.invalid_credentials` · `423` `identity.locked_out` + `lockoutEnd` · `400` `identity.validation_failed`.
- `DELETE /api/v1/session` → `204`.

— `contracts/openapi.yaml, operationIds logIn + logOut, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-04 — happy path

> **Given** a Visitor whose Account exists
> **When** the Visitor logs in with the correct email address (in any letter case, spaces at the edges ignored) and password
> **Then** the system logs them in as their Player and shows their Profile
>
> — `spec.md §5, AC-04, verbatim` · full text: [spec.md](../spec.md)

### AC-05 — error

> **Given** a Visitor on the login form
> **When** the Visitor enters an email address that has no Account, or a wrong password for an existing one
> **Then** the system does not log them in, and shows the same message in both cases ("email or password is incorrect"), so the login form does not reveal which email addresses are registered
>
> — `spec.md §5, AC-05, verbatim` · full text: [spec.md](../spec.md)

### AC-06 — authorization

> **Given** an Account that has had 5 failed login attempts in a row
> **When** anyone tries to log in to that Account again within the next 15 minutes, even with the correct password
> **Then** the system refuses the login and says that login is temporarily blocked and when it can be tried again. After the 15 minutes, a correct login succeeds. The count of failures goes back to zero when the block starts, so after it ends 5 new failures are needed to block again. Attempts during the block do not extend it. A successful login before the 5th failure also resets the count to zero
>
> — `spec.md §5, AC-06, verbatim` · full text: [spec.md](../spec.md)

### AC-07 — happy path

> **Given** a logged-in Player
> **When** the Player logs out
> **Then** the system ends their session in this browser only, shows the portal as a Visitor sees it, and going back to the Profile page asks them to log in. The same Account stays logged in on any other device where it is logged in; being logged in on several devices at once is allowed
>
> — `spec.md §5, AC-07, verbatim` · full text: [spec.md](../spec.md)

### AC-10 — authorization

> **Given** a Visitor who is not logged in
> **When** the Visitor tries to open any Profile, their own or someone else's
> **Then** the system shows no profile details and asks them to log in or register first
>
> — `spec.md §5, AC-10, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `LoginPage`: email + password. On `401`, show the server title "Email or password is incorrect."; on `423`, show the block message with `lockoutEnd` in local time; on `200`, set `me` and navigate to `/profile` — `web/src/features/auth/`
- [ ] `LogoutButton`: `DELETE /api/v1/session` → clear `me` → navigate to `/` — `web/src/features/auth/`
- [ ] `RequireAuth` guard: `useMe` loading → spinner; `null` → a "log in or register first" view with links to `/login` and `/register` — `web/src/features/auth/`
- [ ] Route `/login`; wrap the logged-in routes in `RequireAuth` — `web/src/App.tsx`
- [ ] RTL tests for each state — `web/src/features/auth/*.test.tsx`

## Edge cases

| Case | Behaviour |
|---|---|
| `401` for an unknown email vs a wrong password | The same message is rendered |
| `423` | Message includes when to retry (from `lockoutEnd`) |
| Logout, then the browser Back button to `/profile` | The guard shows "log in or register first" |

## Definition of Done

- [ ] RTL tests for login success, `401`, `423`, logout and the guard pass
- [ ] `npm --prefix web test`, `lint` and `build` clean
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
