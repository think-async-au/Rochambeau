---
id: T7
title: "Register an Account through POST /api/v1/accounts"
layer: "ports"
deps: ["T5"]
blocks: ["T8", "T9", "T11"]
acs: ["AC-01", "AC-02", "AC-03", "AC-14"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/Register/", "src/Rochambeau.Api/Modules/Identity/Endpoints/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 73 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T7 — Register an Account through POST /api/v1/accounts

## Place in the sequence

- **Blocked by:** T5 — Expose ICurrentAccount and IAccountDirectory through Identity Contracts
- **Blocks:** T8 — Enforce the registration cap and prove the race guard on registration · T9 — Log in through POST /api/v1/session with lockout and enumeration-safe errors · T11 — Serve the public Profile through GET /api/v1/profiles/{username}
- **Wave:** 5 — starts once T5 is done.
- **Lane:** shares `Application/Register/` with T8, and `Endpoints/` + `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T9–T11 — serialized.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task delivers the registration flow itself: a valid Visitor becomes a logged-in Player in one submission. Invalid or duplicate input gets field-level answers.

## Inlined context

> Register: trim, then check the username, email and password rules → field errors if any. Otherwise insert the Account with role `player` and a new security stamp. A unique violation → "username taken" or "email cannot be used", without naming the holder. Inserted → session cookie + own profile.
>
> — `sad.md §6, critical flow 1 (cap branches → T8), abridged` · full text: [sad.md](../sad.md)

> `Id` is set with `Guid.CreateVersion7()` in the application layer before `UserManager.CreateAsync`. The database never generates it.
>
> — `adr/0001 §Decision outcome, implementation note 2, verbatim` · full text: [adr/0001-use-identity-core-with-module-owned-mappings.md](../adr/0001-use-identity-core-with-module-owned-mappings.md)

> **Hard rule:** Registration always writes `player`. No HTTP endpoint or hub method accepts a role value (AC-14).
>
> — `adr/0003 §Decision outcome, implementation note 1, verbatim` · full text: [adr/0003-store-one-role-per-account-in-a-column.md](../adr/0003-store-one-role-per-account-in-a-column.md)

> - `ix_accounts_normalized_user_name` → `identity.username_taken`;
> - `ix_accounts_normalized_email` → the «email cannot be used» error.
>
> — `data-model.md §Entities, Unique-violation mapping, verbatim` · full text: [data-model.md](../data-model.md)

> **Hard rule:** **Personal data:** The email appears only in the owner's own-profile response. The public-profile response type and `AccountIdentity` have no email field at all (AC-09, AC-11). All text is rendered as plain text by React, never as HTML
>
> — `sad.md §8, Personal data row, verbatim` · full text: [sad.md](../sad.md)

> Registering while already logged in has no AC yet. Contract default: the new session replaces the old one in this browser.
>
> — `contracts/api-sync-report.md §Open questions, OQ-1, abridged` · full text: [api-sync-report.md](../contracts/api-sync-report.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

| Column | Type | Constraints | Change |
|---|---|---|---|
| `id` | `uuid` | PK `pk_accounts` | written (insert) |
| `user_name` | `varchar(20)` | NOT NULL | written (insert) |
| `normalized_user_name` | `varchar(20)` | NOT NULL, UNIQUE (`ix_accounts_normalized_user_name`) | written (insert) |
| `email` | `varchar(256)` | NOT NULL | written (insert) |
| `normalized_email` | `varchar(256)` | NOT NULL, UNIQUE (`ix_accounts_normalized_email`) | written (insert) |
| `password_hash` | `text` | NOT NULL | written (insert) |
| `security_stamp` | `text` | NOT NULL | written (insert) |
| `role` | `varchar(20)` | NOT NULL, CHECK `ck_accounts_role` | written (insert) |
| `created_at` | `timestamptz` | NOT NULL | written (insert) |

No schema change: the table comes from T3. Identity also writes `email_confirmed = false`, `two_factor_enabled = false`, `lockout_enabled = true`, `access_failed_count = 0`.

— `data-model.md §Entities, table identity.accounts, abridged (columns this task writes)` · full text: [data-model.md](../data-model.md)

## API contract

- `POST /api/v1/accounts` (`registerAccount`, `security: []`, requires `X-Rochambeau-Client`). Body `Registration` {`username`, `email`, `password`}; any other field (e.g. `role`) is ignored, never rejected.
- `201` `OwnProfile` {`username`, `email`, `joinedAt`, `role`} + `Set-Cookie` session + `Location: /api/v1/me`.
- `400` `identity.validation_failed` (ValidationProblemDetails, `errors` keyed `username` / `email` / `password`).
- `409` `identity.username_taken` ("This username is already taken.") · `409` `identity.email_unavailable` ("This email address cannot be used.").

— `contracts/openapi.yaml, operationId registerAccount (429 → T8), abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

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

### AC-14 — authorization

> **Given** a Visitor registering, or a logged-in Player
> **When** they try to give themselves, or anyone else, the administrator role through the portal
> **Then** the system offers no way to do so, and any attempt leaves every Account's role unchanged
>
> — `spec.md §5, AC-14, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] Register command and handler: trim + Domain validation, collecting all field errors → validation `Error`; set `Id = Guid.CreateVersion7()`, `Role = Player`, `CreatedAt` from `TimeProvider`; `UserManager.CreateAsync(account, password)` — `Application/Register/`
- [ ] Map both Identity's `DuplicateUserName` / `DuplicateEmail` errors **and** PostgreSQL `23505` (by index name) to `identity.username_taken` / `identity.email_unavailable` — `Application/Register/`
- [ ] On success, sign in with a persistent cookie (the 7-day sliding lifetime) and return `OwnProfile` with `role` in lower case — `Application/Register/`
- [ ] Endpoint `MapPost("/api/v1/accounts")` with `AllowAnonymous`, a request DTO with exactly the three fields, and `Result` → ProblemDetails via T1 — `Endpoints/`, `IdentityModule.cs`
- [ ] Test helper `RegisterAsync(client, username?, email?, password?)` (defaults `player_<n>`, `user-<uuid>@example.test`, `test-password-8`) — `tests/Rochambeau.IntegrationTests/Identity/`
- [ ] Integration tests for AC-01, AC-02, AC-03 and AC-14 — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Body carries `"role": "administrator"` | `201`, `role: player` (AC-14) |
| `rocky` when `Rocky` exists; `USER-1@example.test` when `user-1@example.test` exists | `409 identity.username_taken` / `409 identity.email_unavailable`, naming no holder |
| Username and email both invalid, password too short | One `400` with three `errors` entries |
| Both username and email already taken | Not specified upstream: either `409` is acceptable. Don't assert which one |
| Browser already logged in (OQ-1) | Contract default: the new session replaces the old one |
| Overlong email (255 characters) | `400` field error, never a 500 (T2 rule) |

## Definition of Done

- [ ] Integration tests for AC-01, AC-02, AC-03 and AC-14 pass against Testcontainers PostgreSQL
- [ ] The `201` body has exactly `username`, `email`, `joinedAt`, `role`, and the cookie is set
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
