---
id: T13
title: "Revalidate the session on every hub invocation with SessionRevalidationHubFilter"
layer: "infra"
deps: ["T12"]
blocks: []
acs: ["AC-08", "AC-13b"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Infrastructure/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Infrastructure/", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 42 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T13 — Revalidate the session on every hub invocation with SessionRevalidationHubFilter

## Place in the sequence

- **Blocked by:** T12 — Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow
- **Blocks:** — (none)
- **Wave:** 9 — starts once T12 is done.
- **Lane:** shares `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T4–T12 — serialized; last backend task.

## Why (user story)

> **As an** Administrator
> **I want** the portal to recognise my administrator role when I log in
> **So that** moderation tools can be offered to me, and only to me, once they exist
>
> — `spec.md §4, US-06, verbatim` · full text: [spec.md](../spec.md)

This task keeps AC-13b true over long-lived real-time connections: after a role change, no open hub connection keeps acting with the old role.

## Inlined context

> **Hard rule:** Claim names, cookies and `identity.*` tables stay private to the module. A global `SessionRevalidationHubFilter` re-checks the security stamp on every hub method invocation and aborts the connection when the session was revoked. This keeps AC-13b true over long-lived WebSocket connections.
>
> — `sad.md §5, Identity seam for other modules, verbatim` · full text: [sad.md](../sad.md)

> - Opening a real-time connection counts as activity (AC-08): the negotiate request goes through the cookie middleware like any other request and renews the sliding cookie.
> - The hub filter does one primary-key read per hub invocation. Any cache for it needs its own ADR (`CLAUDE.md` · Infrastructure).
>
> — `adr/0004 §Decision outcome, implementation notes 3–4, verbatim` · full text: [adr/0004-expose-acting-account-through-identity-contracts.md](../adr/0004-expose-acting-account-through-identity-contracts.md)

> Critical flow 2: hub call → the session revalidation filter asks who is acting → read the security stamp → matches: account id, username, role → after `grant-admin` rotated the stamp: changed → "nobody, abort the connection" → connection closed.
>
> — `sad.md §6, critical flow 2, abridged` · full text: [sad.md](../sad.md)

> The SPA's shared fetch wrapper (`web/src/shared/api/`) always sends the header. The SignalR client sends it on negotiate through its `headers` option. The WebSocket upgrade itself is a GET and is not an unsafe method.
>
> — `adr/0005 §Decision outcome, implementation note 1, verbatim` · full text: [adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md](../adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md)

> **Real-time:** one SignalR hub per module (`/hubs/play`, `/hubs/chat`). The server is authoritative: moves are stored hidden and broadcast only after both are committed; clients never decide outcomes.
>
> — `CLAUDE.md §Conventions, Real-time, verbatim` · full text: [CLAUDE.md](../../../../CLAUDE.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

Internal — no API surface.

## Acceptance criteria

### AC-08 — cross-context

> **Given** a Player who is logged in, and another area of the portal (such as play, chat or moderation) that needs to know who is acting
> **When** that area asks Accounts who is behind the current request
> **Then** Accounts answers with that Player's account identity, username and role, without the Player logging in again. It answers "nobody" for a Visitor, after the Player logs out on that device, after the operator changes the Account's role (AC-13b), or after 7 days without activity. Activity is any request the logged-in Player makes to the portal, including opening a real-time connection. The Player is logged out no earlier than 3.5 days and no later than 7 days after their last activity
>
> — `spec.md §5, AC-08, verbatim` · full text: [spec.md](../spec.md)

### AC-13b — authorization, role change

> **Given** an Account that is logged in on one or more devices
> **When** the portal's operator grants or removes the administrator role on that Account
> **Then** the system ends that Account's sessions on every device, so the owner must log in again and from then on is recognised with the new role. Until then, no device keeps acting with the old role. Removing the administrator role this way is how an Administrator loses their powers; a ban (roadmap step 6) must end sessions on every device in the same way
>
> — `spec.md §5, AC-13b, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `SessionRevalidationHubFilter : IHubFilter`. On connect and on every method invocation: read the account id + stamp claims from `Context.User`, read the current stamp by PK, and on a missing Account or mismatched stamp, abort the connection and log "hub connection aborted after revocation" — `Infrastructure/`
- [ ] Register it globally for every hub from `AddIdentityModule` (`HubOptions.AddFilter`), so no future module can forget it — `IdentityModule.cs`
- [ ] Test-only hub mapped in the test host, plus a SignalR client over the TestServer handler sending the cookie and `X-Rochambeau-Client` on negotiate — `tests/Rochambeau.IntegrationTests/Infrastructure/`
- [ ] Integration tests: a logged-in client connects and invokes successfully; `PromoteToAdministratorAsync` (T12) → the next invocation closes the connection; an anonymous negotiate → `401` — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Anonymous negotiate | `401` via the fallback policy (T4); no connection |
| Negotiate without `X-Rochambeau-Client` | `400 common.client_header_required` (T1) |
| Stamp rotated while connected | The next invocation aborts the connection |
| Account row gone | Treated as revoked — abort |

## Definition of Done

- [ ] Integration test proves AC-13b over a hub connection: the connection is closed on the first invocation after the role change
- [ ] The filter is registered globally (no per-hub opt-in)
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
