---
id: T5
title: "Expose ICurrentAccount and IAccountDirectory through Identity Contracts"
layer: "infra"
deps: ["T4"]
blocks: ["T7"]
acs: ["AC-08"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Contracts/", "src/Rochambeau.Api/Modules/Identity/Infrastructure/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.UnitTests/Identity/", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 41 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T5 — Expose ICurrentAccount and IAccountDirectory through Identity Contracts

## Place in the sequence

- **Blocked by:** T4 — Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule
- **Blocks:** T7 — Register an Account through POST /api/v1/accounts
- **Wave:** 4 — starts once T4 is done.
- **Lane:** shares `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T4, T6–T13 — serialized. The contract interfaces are folded in here together with their first implementations (contract-task rule).

## Why (user story)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

This task builds the one seam through which every other area learns who is acting. It answers an account identity, or "nobody" (AC-08).

## Inlined context

> **Hard rule:** **Chosen:** Option 1. Modules depend on a stable, typed answer («who is acting») instead of the claim layout. A token scheme, 2FA or an extracted Auth service changes only Identity's implementation. The hub filter enforces AC-13b once, for every hub, so no future module can forget it. Option 2 needs less code, but it couples every module to the cookie's claim format and leaves AC-13b to each hub's discipline.
>
> — `adr/0004 §Decision outcome, Chosen, verbatim` · full text: [adr/0004-expose-acting-account-through-identity-contracts.md](../adr/0004-expose-acting-account-through-identity-contracts.md)

> - `ICurrentAccount` resolves the acting person for the current HTTP request or hub invocation to `AccountIdentity(AccountId, Username, Role)`, or to «nobody» for a Visitor (AC-08).
> - `IAccountDirectory` looks an Account up by id or by exact Username, letter case ignored (AC-11, AC-12). It never returns the email.
> - Claim names, cookies and `identity.*` tables stay private to the module. A global `SessionRevalidationHubFilter` re-checks the security stamp on every hub method invocation and aborts the connection when the session was revoked. This keeps AC-13b true over long-lived WebSocket connections.
>
> — `sad.md §5, Identity seam for other modules, verbatim` · full text: [sad.md](../sad.md)

> - `AccountId` is the account's UUIDv7 key. Other modules store it as their reference to a person, never the Username or email (`target-architecture.md` · «Player data is keyed by account identity»).
> - `AccountIdentity` has no email field. The only way to read an email is the owner's own-profile query inside Identity (AC-09, AC-11).
>
> — `adr/0004 §Decision outcome, implementation notes 1–2, verbatim` · full text: [adr/0004-expose-acting-account-through-identity-contracts.md](../adr/0004-expose-acting-account-through-identity-contracts.md)

> The role is exposed to other modules as part of the identity contract (accounts ADR-0004), never as a raw claim name.
>
> — `adr/0003 §Decision outcome, implementation note 3, verbatim` · full text: [adr/0003-store-one-role-per-account-in-a-column.md](../adr/0003-store-one-role-per-account-in-a-column.md)

> Who-is-acting, per request: no session → nobody; session older than 7 days since last renewal → nobody; Account gone or stamp changed → nobody and the stale cookie is removed; stamp matches → (account id, username, role) and the cookie is renewed for 7 days.
>
> — `sad.md §6, Cross-cutting: who is acting, abridged` · full text: [sad.md](../sad.md)

> **How verify:** an architecture unit test in `tests/Rochambeau.UnitTests/Identity/` reflects over the API assembly. It fails if any type outside `Modules/Identity/` references a type in `Identity.Domain`, `Identity.Application`, `Identity.Infrastructure` or `Microsoft.AspNetCore.Identity`. The KPI itself is checked when step 3 is specified.
>
> — `sad.md §10, QG-4 How verify, verbatim` · full text: [sad.md](../sad.md)

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

## Checklist

- [ ] `AccountIdentity(Guid AccountId, string Username, AccountRole Role)` with no email; the contract `AccountRole`; `ICurrentAccount` (returns the identity or "nobody") and `IAccountDirectory` (find by id, and by exact Username ignoring case) — `Contracts/`
- [ ] Claims principal factory: emits the account id, Username, security stamp and the standard role claim (`Administrator` / `Player`) from the `role` column — `Infrastructure/`
- [ ] `CurrentAccount`: maps the authenticated principal (already stamp-validated by the cookie middleware, T4) to `AccountIdentity`, or "nobody" — `Infrastructure/`
- [ ] `AccountDirectory`: normalises the Username with Identity's `ILookupNormalizer` and queries `normalized_user_name` (exact, no partial match) or the PK. Never returns the email — `Infrastructure/`
- [ ] Register them in `AddIdentityModule` — `IdentityModule.cs`
- [ ] Architecture test (QG-4) — `tests/Rochambeau.UnitTests/Identity/`
- [ ] Integration test for `IAccountDirectory`, and unit tests for the principal ↔ `AccountIdentity` mapping — `tests/Rochambeau.IntegrationTests/Identity/`, `tests/Rochambeau.UnitTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Anonymous principal | "nobody" |
| Principal for an Account with role `administrator` | `AccountIdentity` with `Role = Administrator` |
| `FindByUsername("scissors")` when `Scissors` exists | Returns the Account with Username `Scissors` (as typed by the owner) |
| `FindByUsername("sciss")` | "not found" — no partial match |
| A type outside `Modules/Identity/` uses `Microsoft.AspNetCore.Identity` | The architecture test fails |

## Definition of Done

- [ ] Unit tests: principal built by the claims factory → the right `AccountIdentity`; anonymous → "nobody"
- [ ] Integration test: `IAccountDirectory` finds by id and by Username (case ignored), and returns "not found" for unknown and partial values
- [ ] QG-4 architecture test passes
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
