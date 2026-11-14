---
id: T2
title: "Encode the Identity domain rules: username, email, password, role, lockout and cap policy"
layer: "domain"
deps: []
blocks: ["T3", "T6"]
acs: ["AC-02", "AC-03"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/Domain/", "tests/Rochambeau.UnitTests/Identity/"]
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

# T2 — Encode the Identity domain rules: username, email, password, role, lockout and cap policy

## Place in the sequence

- **Blocked by:** — (none)
- **Blocks:** T3 — Map the Account entity to identity.accounts and promote the staged migration · T6 — Implement the in-process RegistrationCap sliding window
- **Wave:** 1 — no prerequisites.
- **Lane:** own lane — framework-free code with no I/O; runs in parallel with T1 and T15.

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task gives registration its field rules (AC-02) and the normalisation that makes "Rocky" and "rocky" the same Username (AC-03), as framework-free values.

## Inlined context

> **Hard rule:** **Module boundaries:** a module calls another only through that module's `Contracts/` namespace — never its entities or tables. Domain has no framework dependencies. No message broker; in-process calls.
>
> — `CLAUDE.md §Conventions, Module boundaries, verbatim` · full text: [CLAUDE.md](../../../../CLAUDE.md)

> `Account` derives from `IdentityUser<Guid>`, so it is an Infrastructure persistence type. The Identity module's `Domain/` holds the framework-free rules: Username format (3–20 Latin letters, digits, `_`, `-`), password length 8–128, and trimming (AC-02).
>
> — `adr/0001 §Decision outcome, implementation note 1, verbatim` · full text: [adr/0001-use-identity-core-with-module-owned-mappings.md](../adr/0001-use-identity-core-with-module-owned-mappings.md)

> **Normalisation:** Trim the username and email before validation (AC-02). Usernames are Latin letters a–z/A–Z, digits, `_` and `-` only, so normalisation is unambiguous. Case-insensitive uniqueness comes from unique indexes on Identity's normalised columns (AC-03, AC-03b)
>
> — `sad.md §8, Normalisation row, verbatim` · full text: [sad.md](../sad.md)

> Email maximum length: AC-02 sets none, and the column is `varchar(256)`. The contract fixes `maxLength: 254` (RFC 5321 path limit), so the Domain email rule must reject anything longer. Otherwise an overlong email reaches the database as a 500 instead of a field error.
>
> — `contracts/api-sync-report.md §Findings, F-3 + data-model.md §Entities, TBD email length, abridged` · full text: [api-sync-report.md](../contracts/api-sync-report.md)

> Policy values: lockout = 5 consecutive failures → blocked 15 minutes (AC-06). Registration cap = 5 successful registrations per network address per 60 minutes (AC-15). Roles = `player` | `administrator` (`moderator` later), one per Account.
>
> — `sad.md §8 Lockout + Registration cap rows, §4 choice 3, abridged` · full text: [sad.md](../sad.md)

> Field messages the contract shows: "The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen." · "The password must be at least 8 characters."
>
> — `contracts/openapi.yaml, registerAccount 400 example, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

Internal — no API surface.

## Acceptance criteria

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

## Checklist

- [ ] `Username`: trim → 3–20 characters, `^[A-Za-z0-9_-]+$` (ASCII only), and a field error message saying what it must be — `Domain/`
- [ ] `EmailAddress`: trim → well-formed → ≤254 characters, with a field message. Use framework-free BCL code only — `Domain/`
- [ ] `Password`: length 8–128 is the only rule (no trimming, no character classes), with a field message — `Domain/`
- [ ] `AccountRole` { `Player`, `Administrator` }, plus `LockoutPolicy` (5, 15 min) and `RegistrationCapPolicy` (5, 60 min) constants — `Domain/`
- [ ] Validation returns **all** field errors at once (username, email, password), so the endpoint can answer with one response — `Domain/`
- [ ] Unit tests for every boundary and edge case below — `tests/Rochambeau.UnitTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| Username of 2 / 3 / 20 / 21 characters | reject / accept / accept / reject |
| Username `Rоcky` with a Cyrillic "о" | Rejected — only Latin a–z, A–Z (AC-02) |
| `"  Rocky  "` | Accepted as `Rocky` (trimmed before checking) |
| Password of 7 / 8 / 128 / 129 characters | reject / accept / accept / reject; `"12345678"` with no letters is accepted |
| Email of 255 characters, or with no `@` | Rejected with the email field message |
| `" User@Example.Test "` | Accepted. It is trimmed, and case is kept for display (normalisation for comparison is Identity's, T3/T4) |

## Definition of Done

- [ ] Unit tests cover every boundary in the edge table and pass
- [ ] `Domain/` has no `using` of ASP.NET Core, EF Core or `Microsoft.AspNetCore.Identity`
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
