---
id: T9
title: "Log in through POST /api/v1/session with lockout and enumeration-safe errors"
layer: "ports"
deps: ["T7"]
blocks: ["T10", "T14"]
acs: ["AC-04", "AC-05", "AC-06"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Application/LogIn/", "src/Rochambeau.Api/Modules/Identity/Endpoints/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 58 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T9 — Log in through POST /api/v1/session with lockout and enumeration-safe errors

## Place in the sequence

- **Blocked by:** T7 — Register an Account through POST /api/v1/accounts
- **Blocks:** T10 — Serve GET /api/v1/me and log out with DELETE /api/v1/session · T14 — Measure the spec §6 latency targets for register, log in and view a profile
- **Wave:** 6 — starts once T7 is done.
- **Lane:** shares `Endpoints/` + `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T7, T10, T11 — serialized.

## Why (user story)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

This task lets a returning Player log in by email and password, without revealing which emails exist, and blocks password guessing.

## Inlined context

> Log in: trim and normalise the email → look up the Account by normalised email. **None** → verify the password against a fixed dummy hash, so timing matches → "email or password is incorrect". **Locked and the 15 minutes not passed** → "temporarily blocked, try again after the lockout end", with no write. **Wrong password** → 5th failure in a row: set lockout end = now + 15 min, reset the count to 0, answer "blocked"; fewer: count + 1, answer "incorrect". **Correct** → reset the count to 0, issue a session carrying account id, username, role and stamp.
>
> — `sad.md §6, Log in, with lockout, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** **Account enumeration:** An unknown email and a wrong password return the same `identity.invalid_credentials` error (AC-05). For an unknown email the server still verifies the password against a fixed dummy hash, so response time does not reveal which emails exist
>
> — `sad.md §8, Account enumeration row, verbatim` · full text: [sad.md](../sad.md)

> **Lockout:** Identity lockout: 5 consecutive failures → locked for 15 minutes. The count resets when the lock starts and on a successful login; attempts during the lock neither count nor extend it (AC-06)
>
> — `sad.md §8, Lockout row, verbatim` · full text: [sad.md](../sad.md)

> The lockout answer on the 5th consecutive failure itself is drawn as «temporarily blocked», which is what Identity returns. AC-06 only requires the block from the next attempt on, so this is stricter, not contrary.
>
> — `sad.md §6, Flags for design, verbatim` · full text: [sad.md](../sad.md)

> **Time:** A single `TimeProvider` registration, read by the application layer, Identity lockout and cookie expiry. Integration tests swap in a fake clock for the session-lifetime and lockout checks (spec §6)
>
> — `sad.md §8, Time row, verbatim` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

| Column | Type | Constraints | Change |
|---|---|---|---|
| `normalized_email` | `varchar(256)` | NOT NULL, UNIQUE (`ix_accounts_normalized_email`) | read (unique lookup) |
| `lockout_end` | `timestamptz` | NULL | written |
| `access_failed_count` | `integer` | NOT NULL | written |

No schema change.

— `data-model.md §Entities + §Indexes, table identity.accounts, abridged` · full text: [data-model.md](../data-model.md)

## API contract

- `POST /api/v1/session` (`logIn`, `security: []`, requires `X-Rochambeau-Client`). Body `Credentials` {`email`, `password`}, both non-empty strings.
- `200` `OwnProfile` + `Set-Cookie` session.
- `400` `identity.validation_failed` only for a missing/empty field. A malformed email → `401`, like an unknown one.
- `401` `identity.invalid_credentials`, title "Email or password is incorrect." The body is identical for an unknown email and a wrong password.
- `423` `identity.locked_out` (`LockoutProblemDetails` + `lockoutEnd` UTC) + `Retry-After` seconds, both on the 5th failure itself and during the block.

— `contracts/openapi.yaml, operationId logIn, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

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

## Checklist

- [ ] LogIn handler implementing every branch above, with the dummy-hash verify on an unknown email. Use `SignInManager`/`UserManager` lockout (`lockoutOnFailure: true`) and a persistent cookie on success. Log "login failed" / "account locked out" keyed by account id — `Application/LogIn/`
- [ ] Endpoint `MapPost("/api/v1/session")` with `AllowAnonymous`, `Retry-After` on `423`, and `OwnProfile` reused from T7 — `Endpoints/`, `IdentityModule.cs`
- [ ] Check that Identity's lockout reads the injected `TimeProvider`. If it does not, the fake-clock test cannot pass; fix the wiring rather than the test (sad §8 Time) — `IdentityModule.cs`
- [ ] Integration tests for AC-04, AC-05 (compare both bodies and `errorCode`) and AC-06 with a fake clock — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Email `"  USER-1@Example.Test "` for `user-1@example.test` | `200` (AC-04) |
| Malformed email `"not-an-email"` | `401 identity.invalid_credentials`, not `400` |
| 4 failures, then success, then 4 failures | Still `401` — success reset the count |
| Correct password during the block | `423` with the same `lockoutEnd` (attempts don't extend it) |
| Correct password at lockout end + 1 s | `200` |
| 5 new failures after a block ended | Blocked again (the count was reset when the block started) |

## Definition of Done

- [ ] Integration tests for AC-04, AC-05, AC-06 pass; the AC-06 test uses the fake `TimeProvider`
- [ ] Unknown-email and wrong-password responses are byte-identical in status, body and `errorCode`
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
