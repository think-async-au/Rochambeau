---
id: T15
title: "Add the shared API client and the «me» query to the SPA"
layer: "ui"
deps: []
blocks: ["T16"]
acs: ["AC-08"]
files_hint: ["web/src/shared/api/"]
owner: "a.udalov"
estimate: "S"
context_budget: "S"   # measured: 38 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T15 — Add the shared API client and the «me» query to the SPA

## Place in the sequence

- **Blocked by:** — (none)
- **Blocks:** T16 — Build the Register screen with field-level errors
- **Wave:** 1 — no prerequisites.
- **Lane:** own lane — wave 1, parallel with the backend. It works from the contract only; tests mock `fetch`.

## Why (user story)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

This task gives the SPA one place that knows whether this browser is a Visitor or a Player. Every screen asks it.

## Inlined context

> ├── shared/api/           fetch wrapper (same-origin cookie, X-Rochambeau-Client header, ProblemDetails parsing), "me" query
>
> — `sad.md §5, Internal decomposition (web), verbatim` · full text: [sad.md](../sad.md)

> **Hard rule:** The SPA's shared fetch wrapper (`web/src/shared/api/`) always sends the header. The SignalR client sends it on negotiate through its `headers` option. The WebSocket upgrade itself is a GET and is not an unsafe method.
>
> — `adr/0005 §Decision outcome, implementation note 1, verbatim` · full text: [adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md](../adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md)

> **Hard rule:** **Frontend:** Tailwind utility classes only (tokens in `web/src/index.css` `@theme`); TanStack Query for server state; React Router for routing; no global store until one is needed.
>
> — `CLAUDE.md §Conventions, Frontend, verbatim` · full text: [CLAUDE.md](../../../../CLAUDE.md)

> After logout the UI forgets the cached own identity. A request that answers "not logged in" makes the Web client ask to log in again.
>
> — `sad.md §6, Log out + critical flow 2, abridged` · full text: [sad.md](../sad.md)

> No `screens.md` and no `docs/design-system.md` exist for this feature. `web/src/main.tsx` already provides `QueryClientProvider` and `BrowserRouter`: reuse them, don't add another.
>
> — `repo state at breakdown (web/src/main.tsx), abridged` · full text: [web/src/main.tsx](../../../../web/src/main.tsx)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `GET /api/v1/me` → `200` `OwnProfile` {`username`, `email`, `joinedAt`, `role`: `player`|`administrator`} · `401` `identity.not_authenticated` (= Visitor).
- Errors: `application/problem+json` ProblemDetails {`type`, `title`, `status`, `errorCode`}; ValidationProblemDetails adds `errors` {field: string[]}; LockoutProblemDetails adds `lockoutEnd`.
- Header `X-Rochambeau-Client: web` (required on unsafe requests).

— `contracts/openapi.yaml, getOwnProfile + components.schemas, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-08 — cross-context

> **Given** a Player who is logged in, and another area of the portal (such as play, chat or moderation) that needs to know who is acting
> **When** that area asks Accounts who is behind the current request
> **Then** Accounts answers with that Player's account identity, username and role, without the Player logging in again. It answers "nobody" for a Visitor, after the Player logs out on that device, after the operator changes the Account's role (AC-13b), or after 7 days without activity. Activity is any request the logged-in Player makes to the portal, including opening a real-time connection. The Player is logged out no earlier than 3.5 days and no later than 7 days after their last activity
>
> — `spec.md §5, AC-08, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `apiFetch(path, init)`: same-origin credentials, always sends `X-Rochambeau-Client: web`, JSON in/out; a non-2xx `problem+json` throws a typed `ApiError` {`status`, `errorCode`, `title`, `errors?`, `lockoutEnd?`} — `web/src/shared/api/`
- [ ] Types `OwnProfile`, `PublicProfile`, `AccountRole`, mirroring the contract — `web/src/shared/api/`
- [ ] `useMe()` (TanStack Query key `['me']`): resolves to `OwnProfile`, or `null` on `401` (not an error, no retry); plus helpers to set and clear the cached identity — `web/src/shared/api/`
- [ ] Vitest tests with a mocked `fetch` — `web/src/shared/api/*.test.ts`

## Edge cases

| Case | Behaviour |
|---|---|
| `/me` → `401` | `useMe` data is `null` (Visitor), not an error state |
| `/me` → `500` / network error | Error state (not treated as logged out) |
| `400` with `errors` | `ApiError.errors` keeps the field map as-is |
| `423` body | `ApiError.lockoutEnd` parsed as a date |

## Definition of Done

- [ ] Vitest tests: header always sent; `401` → `null`; ProblemDetails, `errors` and `lockoutEnd` parsed
- [ ] `npm --prefix web run lint` and `build` clean
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
