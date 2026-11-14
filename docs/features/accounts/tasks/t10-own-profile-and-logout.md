---
id: T10
title: "Serve GET /api/v1/me and log out with DELETE /api/v1/session"
layer: "ports"
deps: ["T9"]
blocks: ["T12"]
acs: ["AC-07", "AC-08", "AC-09", "AC-10"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/Profiles/", "src/Rochambeau.Api/Modules/Identity/Endpoints/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 57 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T10 — Serve GET /api/v1/me and log out with DELETE /api/v1/session

## Place in the sequence

- **Blocked by:** T9 — Log in through POST /api/v1/session with lockout and enumeration-safe errors
- **Blocks:** T12 — Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow
- **Wave:** 7 — starts once T9 is done.
- **Lane:** shares `Application/Profiles/` with T11, and `Endpoints/` + `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T7–T11 — serialized.

## Why (user story)

> **As a** Player
> **I want** to log out
> **So that** nobody else using this browser can act as me
>
> — `spec.md §4, US-03, verbatim` · full text: [spec.md](../spec.md)

> **As a** Player
> **I want** to see my Profile, including my private email address
> **So that** I can check how my account looks and which details the portal holds about me
>
> — `spec.md §4, US-04, verbatim` · full text: [spec.md](../spec.md)

This task gives the Player their own Profile, which is also the SPA's "who am I" query, and a log-out that affects this browser only. It proves the AC-08 session lifetime end to end.

## Inlined context

> **Chosen:** Option 1. It meets AC-07, AC-08 and AC-13b with framework mechanisms only: no new table and no cleanup job. The one thing it gives up (a copied cookie outliving logout) is a residual risk the spec already accepted (§6.1). Both options cost one database read per authenticated request. Option 2 stays the upgrade path if «log out everywhere» or a session list is ever needed.
>
> — `adr/0002 §Decision outcome, Chosen, verbatim` · full text: [adr/0002-validate-cookie-security-stamp-on-every-request.md](../adr/0002-validate-cookie-security-stamp-on-every-request.md)

> **Hard rule:** Log out: end the session held by this browser only → logged out, cookie removed. No data-store write: the security stamp is **not** rotated. Other devices of the same Account stay logged in.
>
> — `sad.md §6, Log out, abridged` · full text: [sad.md](../sad.md)

> Own Profile: resolve who is acting (ICurrentAccount, T5) → nobody: "not logged in, no profile details" → a Player: read the Account by PK → username, email, join date, role.
>
> — `sad.md §6, See my own Profile, abridged` · full text: [sad.md](../sad.md)

> **Session lifetime:** logged in until logout; logged out no earlier than 3.5 and no later than 7 days after the last activity (AC-08) — measured as: integration test with a controlled clock: still logged in at 3.5 days − 1 minute, logged out at 7 days + 1 minute
>
> — `spec.md §6, NFR Session lifetime, verbatim` · full text: [spec.md](../spec.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `GET /api/v1/me` (`getOwnProfile`) → `200` `OwnProfile` {`username`, `email`, `joinedAt`, `role`} (closed shape) · `401` `identity.not_authenticated`.
- `DELETE /api/v1/session` (`logOut`, requires `X-Rochambeau-Client`) → `204` + cookie cleared · `400` `common.client_header_required` · `401` `identity.not_authenticated`.

— `contracts/openapi.yaml, operationIds getOwnProfile + logOut, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-07 — happy path

> **Given** a logged-in Player
> **When** the Player logs out
> **Then** the system ends their session in this browser only, shows the portal as a Visitor sees it, and going back to the Profile page asks them to log in. The same Account stays logged in on any other device where it is logged in; being logged in on several devices at once is allowed
>
> — `spec.md §5, AC-07, verbatim` · full text: [spec.md](../spec.md)

### AC-08 — cross-context

> **Given** a Player who is logged in, and another area of the portal (such as play, chat or moderation) that needs to know who is acting
> **When** that area asks Accounts who is behind the current request
> **Then** Accounts answers with that Player's account identity, username and role, without the Player logging in again. It answers "nobody" for a Visitor, after the Player logs out on that device, after the operator changes the Account's role (AC-13b), or after 7 days without activity. Activity is any request the logged-in Player makes to the portal, including opening a real-time connection. The Player is logged out no earlier than 3.5 days and no later than 7 days after their last activity
>
> — `spec.md §5, AC-08, verbatim` · full text: [spec.md](../spec.md)

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

## Checklist

- [ ] GetOwnProfile handler: `ICurrentAccount` → read Account by PK → `OwnProfile` — `Application/Profiles/`
- [ ] Endpoints `MapGet("/api/v1/me")` and `MapDelete("/api/v1/session")` (`SignOutAsync`, `204`) — `Endpoints/`, `IdentityModule.cs`
- [ ] Integration tests: AC-09; AC-10 (anonymous `/me` → `401` with no profile fields); AC-07 (two clients on one Account — one logs out → its `/me` `401`, the other's `200`) — `tests/Rochambeau.IntegrationTests/Identity/`
- [ ] AC-08 / NFR session-lifetime test with the fake clock: last activity, then still `200` at 3.5 days − 1 minute (that request renews), then `401` at 7 days + 1 minute after the last activity — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| `DELETE /api/v1/session` without a session | `401 identity.not_authenticated` |
| Logout on device A | Device B's `/me` stays `200` (no stamp rotation) |
| Back to `/me` after logout with the old cookie removed | `401` |
| Request at 3.5 d − 1 min, then idle | Logout lands 7 days after **that** request |

## Definition of Done

- [ ] Integration tests for AC-07, AC-08 (session-lifetime with fake clock), AC-09 and AC-10 pass
- [ ] The `/me` response has exactly `username`, `email`, `joinedAt`, `role`
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
