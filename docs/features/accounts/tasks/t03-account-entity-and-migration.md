---
id: T3
title: "Map the Account entity to identity.accounts and promote the staged migration"
layer: "migration"
deps: ["T2"]
blocks: ["T4"]
acs: ["AC-03", "AC-03b"]
files_hint: ["docs/features/accounts/migrations/01_create_accounts.up.sql", "docs/features/accounts/migrations/01_create_accounts.down.sql", "src/Rochambeau.Api/Modules/Identity/Infrastructure/Persistence/", "src/Rochambeau.Api/Common/Persistence/Migrations/", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 68 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T3 — Map the Account entity to identity.accounts and promote the staged migration

## Place in the sequence

- **Blocked by:** T2 — Encode the Identity domain rules: username, email, password, role, lockout and cap policy
- **Blocks:** T4 — Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule
- **Wave:** 2 — starts once T2 is done.
- **Lane:** migration lane (serialized by `implement`).

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task creates the table whose two unique indexes are the only guard behind "one Account per Username and per email", even under a race (AC-03, AC-03b).

## Inlined context

> **Hard rule:** **Chosen:** Option 1. It keeps `AppDbContext` and `Common/` free of any Identity type, so the module boundary of repo ADR-0002 and the extraction seam of repo ADR-0004 hold. It still reuses Identity's password hasher, lockout, normalisation and security stamp. Option 2 is less code, but it makes the shared kernel depend on the Identity module's `Account` type and exposes `DbSet<Account>` to every module.
>
> — `adr/0001 §Decision outcome, Chosen, verbatim` · full text: [adr/0001-use-identity-core-with-module-owned-mappings.md](../adr/0001-use-identity-core-with-module-owned-mappings.md)

> - `Id` is set with `Guid.CreateVersion7()` in the application layer before `UserManager.CreateAsync`. The database never generates it.
> - The `NormalizedEmail` index is configured **unique**, because Identity's default index is not. `NormalizedUserName` stays unique. These two database constraints are what make AC-03b hold under a race.
>
> — `adr/0001 §Decision outcome, implementation notes 2–3, verbatim` · full text: [adr/0001-use-identity-core-with-module-owned-mappings.md](../adr/0001-use-identity-core-with-module-owned-mappings.md)

> **Hard rule:** **Persistence:** one `AppDbContext`; each module adds `IEntityTypeConfiguration<T>` classes mapped to its own schema (`identity`, `play`, `chat`, `admin`); snake_case names. No stored procedures or triggers. Every migration has a working `Down`. Deployments run `dotnet ef database update` explicitly — only Development auto-migrates.
>
> — `CLAUDE.md §Conventions, Persistence, verbatim` · full text: [CLAUDE.md](../../../../CLAUDE.md)

> **Unique-violation mapping:** a race that reaches the database (AC-03b) surfaces as PostgreSQL error `23505`. Its constraint name tells the two cases apart:
> - `ix_accounts_normalized_user_name` → `identity.username_taken`;
> - `ix_accounts_normalized_email` → the «email cannot be used» error.
>
> Keep these index names stable. Application code matches on them.
>
> — `data-model.md §Entities, Unique-violation mapping, verbatim` · full text: [data-model.md](../data-model.md)

> **Not mapped (`builder.Ignore`):** `PhoneNumber`, `PhoneNumberConfirmed`. Spec §6.1 lists no phone number as personal data, so the database is kept unable to hold one. They come back through an expand migration if a later step needs them.
>
> — `data-model.md §Entities, Not mapped, verbatim` · full text: [data-model.md](../data-model.md)

> `role` is written in lower case by an EF value converter from `AccountRole` (T2). The CHECK `ck_accounts_role` bounds it to `player` | `administrator`.
>
> — `data-model.md §Entities, role row, abridged` · full text: [data-model.md](../data-model.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

| Column | Type | Constraints | Change |
|---|---|---|---|
| `id` | `uuid` | PK `pk_accounts` | added |
| `user_name` | `varchar(20)` | NOT NULL | added |
| `normalized_user_name` | `varchar(20)` | NOT NULL, UNIQUE (`ix_accounts_normalized_user_name`) | added |
| `email` | `varchar(256)` | NOT NULL | added |
| `normalized_email` | `varchar(256)` | NOT NULL, UNIQUE (`ix_accounts_normalized_email`) | added |
| `email_confirmed` | `boolean` | NOT NULL | added |
| `password_hash` | `text` | NOT NULL | added |
| `security_stamp` | `text` | NOT NULL | added |
| `concurrency_stamp` | `text` | NOT NULL, EF concurrency token | added |
| `two_factor_enabled` | `boolean` | NOT NULL | added |
| `lockout_enabled` | `boolean` | NOT NULL | added |
| `lockout_end` | `timestamptz` | NULL | added |
| `access_failed_count` | `integer` | NOT NULL | added |
| `role` | `varchar(20)` | NOT NULL, CHECK `ck_accounts_role` | added |
| `created_at` | `timestamptz` | NOT NULL | added |

Plus `CHECK (role IN ('player', 'administrator'))` named `ck_accounts_role`, and schema `identity`. No FKs.

Staged pair (reference DDL; `implement` promotes it): `docs/features/accounts/migrations/01_create_accounts.up.sql` / `01_create_accounts.down.sql` → EF migration `<yyyyMMddHHmmss>_AddIdentityAccounts` under `src/Rochambeau.Api/Common/Persistence/Migrations/`. EF's generated `Down` drops the table but **not** the schema: add `migrationBuilder.DropSchema("identity")`.

— `data-model.md §Entities, table identity.accounts, abridged (Notes column dropped)` · full text: [data-model.md](../data-model.md)

## API contract

Internal — no API surface.

## Acceptance criteria

### AC-03 — domain invariant

> **Given** an Account with the username "Rocky" already exists
> **When** a Visitor tries to register with the username "rocky" (any letter case), or with an email address that an existing Account already uses
> **Then** the system creates no Account and tells the Visitor that this username is already taken, or that this email address cannot be used, without revealing whose Account holds it. Usernames are compared ignoring letter case and shown as the owner typed them; email addresses are compared ignoring letter case
>
> — `spec.md §5, AC-03, verbatim` · full text: [spec.md](../spec.md)

### AC-03b — domain invariant, concurrent

> **Given** two Visitors who both choose the username "Paper" (in any letter case, e.g. "Paper" and "paper") or the same email address at the same moment, and no Account holds it yet
> **When** both submit their registration
> **Then** exactly one Account is created with that username (or email address), and the other Visitor is told the username is already taken (or the email address cannot be used)
>
> — `spec.md §5, AC-03b, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `Account : IdentityUser<Guid>` with `Role` (`AccountRole`) and `CreatedAt` — `Infrastructure/Persistence/`
- [ ] `AccountConfiguration : IEntityTypeConfiguration<Account>`: table `accounts` in schema `identity`, max lengths per the table, both unique indexes with the **exact** names, the CHECK, the lower-case role converter, the concurrency token, and `Ignore` for the phone columns. `ApplyConfigurationsFromAssembly` picks it up, and `AppDbContext` stays untouched — `Infrastructure/Persistence/`
- [ ] Run `dotnet ef migrations add AddIdentityAccounts --project src/Rochambeau.Api --output-dir Common/Persistence/Migrations`, then add `DropSchema("identity")` to `Down` — `Common/Persistence/Migrations/`
- [ ] Check that `dotnet ef migrations script` for the new migration is schema-equivalent to the staged `01_create_accounts.up.sql` — staged pair
- [ ] Integration test: two rows with equal `normalized_user_name` (and, separately, `normalized_email`) fail with PostgreSQL `23505` naming the expected index; a raw `role = 'moderator'` fails the CHECK — `tests/Rochambeau.IntegrationTests/Identity/`

## Edge cases

| Case | Behaviour |
|---|---|
| `role` value outside the set | CHECK violation `ck_accounts_role` |
| `lockout_end` NULL | Allowed: never locked |
| `database update 0` with something else left in `identity` | `DROP SCHEMA` (RESTRICT) fails loudly — intended |
| An Identity package bump adds `IdentityUser` columns | They show up in the migration diff. Review them, don't auto-accept (sad §11) |

## Definition of Done

- [ ] Staged migration promoted to the live `Common/Persistence/Migrations/`; `dotnet ef database update` applies it and `dotnet ef database update 0` reverts it, leaving no `identity` schema
- [ ] Integration test proves both unique indexes and the CHECK, with the exact constraint names
- [ ] `AppDbContext` has no reference to `Account`
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
