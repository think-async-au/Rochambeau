---
id: T16
title: "Build the Register screen with field-level errors"
layer: "ui"
deps: ["T15"]
blocks: ["T17"]
acs: ["AC-01", "AC-02", "AC-03", "AC-15"]
files_hint: ["web/src/features/auth/", "web/src/shared/ui/", "web/src/App.tsx"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 51 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T16 — Build the Register screen with field-level errors

## Place in the sequence

- **Blocked by:** T15 — Add the shared API client and the «me» query to the SPA
- **Blocks:** T17 — Build the Log-in screen, the log-out action and the logged-in route guard
- **Wave:** 2 — starts once T15 is done.
- **Lane:** shares `web/src/features/auth/` + `web/src/App.tsx` with T17/T18 — serialized.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task is the one form a Visitor submits to become a logged-in Player. It drives the ≤ 60 s / ≤ 1 submission demo KPI.

## Inlined context

> **UI architecture (web-frontend): client-side SPA.** React + React Router + TanStack Query, same-origin with the API. This is already fixed by repo ADR-0001 and `architecture-map.md` · Frontend, so it needs no feature ADR (the alternatives are excluded by that constraint). Screens go in `web/src/features/auth/` and `web/src/features/profile/` and are composed from `web/src/shared/ui/`. There is no `docs/design-system.md` yet, so screen-level design belongs to `/sdd:design-system` and `/sdd:screens`, not to this SAD.
>
> — `sad.md §4, UI architecture, verbatim` · full text: [sad.md](../sad.md)

> Web branches: field errors → "shows errors, keeps input except the password". Cap → "shows the cap message". Taken → "shows which one, without naming the holder". Inserted → "shows own Profile, logged in as Player".
>
> — `sad.md §6, critical flow 1 (Web lines), abridged` · full text: [sad.md](../sad.md)

> **Screen contract (no `screens.md`, no design system yet; breakdown default):** routes `/register`, `/login`, `/profile` (own), `/players/:username` (public). States for this screen: default · submitting · field errors · conflict · cap · success → navigate to `/profile`. `web/src/shared/ui/` is empty and the `@theme` has no tokens, so create minimal `TextField`, `Button`, `FormError` primitives there with Tailwind default utilities (T17/T18 reuse them). Tokens belong to `/sdd:design-system`.
>
> — `repo state at breakdown + sad.md §4, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** All text is shown as plain text, never interpreted.
>
> — `spec.md §6.1, Abuse cases, abridged` · full text: [spec.md](../spec.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `POST /api/v1/accounts` {`username`, `email`, `password`} → `201` `OwnProfile` · `400` `identity.validation_failed` + `errors` · `409` `identity.username_taken` / `identity.email_unavailable` · `429` `identity.registration_cap`. Show the server `title` / `errors` text.

— `contracts/openapi.yaml, operationId registerAccount, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-01 — happy path

> **Given** a Visitor who is not logged in
> **When** the Visitor registers with a username, an email address and a password that are all valid and not yet taken
> **Then** the system creates the Account with the Player role, logs the Visitor in as that Player straight away, and shows their Profile
>
> — `spec.md §5, AC-01, verbatim` · full text: [spec.md](../spec.md)

### AC-02 — error

> **Given** a Visitor on the registration form
> **When** the Visitor submits a username shorter than 3 or longer than 20 characters, or with characters other than letters, digits, underscore or hyphen, or an email address that is not well formed, or a password shorter than 8 or longer than 128 characters
> **Then** the system creates no Account, and shows next to each offending field what it must be (for example "the password must be at least 8 characters"), keeping what the Visitor typed except the password. "Letters" means the Latin letters a–z and A–Z only, so names that only look alike (e.g. with a Cyrillic "о") cannot be registered. Spaces at the start or end of the username and email are trimmed before checking. Length is the only password rule: no required digits, capitals or symbols
>
> — `spec.md §5, AC-02, verbatim` · full text: [spec.md](../spec.md)

### AC-03 — domain invariant

> **Given** an Account with the username "Rocky" already exists
> **When** a Visitor tries to register with the username "rocky" (any letter case), or with an email address that an existing Account already uses
> **Then** the system creates no Account and tells the Visitor that this username is already taken, or that this email address cannot be used, without revealing whose Account holds it. Usernames are compared ignoring letter case and shown as the owner typed them; email addresses are compared ignoring letter case
>
> — `spec.md §5, AC-03, verbatim` · full text: [spec.md](../spec.md)

### AC-15 — authorization

> **Given** 5 Accounts have already been registered successfully from the same network address within the last 60 minutes (failed attempts do not count)
> **When** a Visitor from that address tries to register another Account
> **Then** the system creates no Account and tells the Visitor that too many accounts were created from their connection recently and to try again later
>
> — `spec.md §5, AC-15, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `TextField` (label, input, error text), `Button`, `FormError` — `web/src/shared/ui/`
- [ ] `RegisterPage`: username, email and password fields with one submit. On `400`, show each `errors[field]` next to its field, keep username and email, and clear the password. On `409`, show the message under the matching field. On `429`, show a form-level message. On `201`, set the `me` cache and navigate to `/profile` — `web/src/features/auth/`
- [ ] Route `/register` — `web/src/App.tsx`
- [ ] RTL tests for each state with a mocked API — `web/src/features/auth/*.test.tsx`

## Edge cases

| Case | Behaviour |
|---|---|
| Several field errors | All shown at once, each next to its field |
| `409 username_taken` | Message under username; email and password inputs unchanged except the password is cleared |
| `429` | Form-level "too many accounts … try again later" |
| Double click on submit | One request (button disabled while submitting) |

## Definition of Done

- [ ] RTL tests for success, field errors (password cleared, other input kept), both conflicts and the cap pass
- [ ] `npm --prefix web test`, `lint` and `build` clean
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
