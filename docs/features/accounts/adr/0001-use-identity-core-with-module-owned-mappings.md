---
status: Accepted
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
ticket: "docs/features/accounts/spec.md"
---

# 0001 — Use ASP.NET Core Identity core with entity mappings owned by the Identity module

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), during the `design` Socratic walk for accounts

## Context

Accounts is the first feature that stores data, and the first one that authenticates anyone. The repo convention already picks ASP.NET Core Identity with cookies (`CLAUDE.md` · Auth). Repo ADR-0003 fixes one `AppDbContext`, where each module contributes `IEntityTypeConfiguration` classes mapped to its own schema. The usual Identity setup makes the shared context inherit `IdentityDbContext`, which would put an Identity-module type into `Common/`. We have to decide how the two fit together before the first `identity.*` migration exists (SAD §4).

## Decision drivers

- Module isolation: `Common/` and other modules must not depend on a module's entities (repo ADR-0002, `CLAUDE.md` · Conventions).
- Keep a seam so Identity can later become the target «Auth / User service» (repo ADR-0004, `target-architecture.md` · Target services).
- Built-in support for what the spec asks: lockout after 5 failures for 15 minutes (AC-06), case-insensitive unique Username/email (AC-03, AC-03b), invalidating every session on a role change (AC-13b), and protected password storage (spec §6.1).
- Solo capacity, 1–2 h/day (`idea-brief.md` §5): reuse framework mechanisms rather than hand-building them.

## Considered options

1. **Identity core inside the module.** `AddIdentityCore<Account>()` + `AddSignInManager()`; Identity's EF `UserStore` runs over the plain `AppDbContext`; the Identity module maps `Account : IdentityUser<Guid>` (and any Identity side tables it uses) with its own `IEntityTypeConfiguration` classes into the `identity` schema.
2. **`AppDbContext` inherits `IdentityDbContext<Account, IdentityRole<Guid>, Guid>`.** The documented default. The schema is set in `OnModelCreating`, and the whole Identity model comes for free.

## Decision outcome

**Chosen:** Option 1. It keeps `AppDbContext` and `Common/` free of any Identity type, so the module boundary of repo ADR-0002 and the extraction seam of repo ADR-0004 hold. It still reuses Identity's password hasher, lockout, normalisation and security stamp. Option 2 is less code, but it makes the shared kernel depend on the Identity module's `Account` type and exposes `DbSet<Account>` to every module.

Implementation notes that follow from this choice:
- `Account` derives from `IdentityUser<Guid>`, so it is an Infrastructure persistence type. The Identity module's `Domain/` holds the framework-free rules: Username format (3–20 Latin letters, digits, `_`, `-`), password length 8–128, and trimming (AC-02).
- `Id` is set with `Guid.CreateVersion7()` in the application layer before `UserManager.CreateAsync`. The database never generates it.
- The `NormalizedEmail` index is configured **unique**, because Identity's default index is not. `NormalizedUserName` stays unique. These two database constraints are what make AC-03b hold under a race.
- Only the Identity side tables this feature needs are mapped. Login and token tables arrive with 2FA (roadmap step 13) or token login.

## Consequences

**Positive**
- Lockout (AC-06), PBKDF2 password hashing, normalised case-insensitive lookups (AC-03, AC-04, AC-11) and the security stamp (AC-13b, accounts ADR-0002) need no custom code.
- `Common/` stays module-agnostic, and other modules cannot reach `identity.*` through a typed `DbSet` property.
- 2FA (roadmap step 13) can reuse Identity's authenticator-token support later.

**Negative**
- The module has to reproduce the mapping work that `IdentityDbContext.OnModelCreating` does (keys, lengths, indexes, concurrency stamp), table by table.
- The persistence entity inherits a framework type, so «Domain has no framework dependencies» is met by keeping `Account` out of `Domain/` rather than by a pure aggregate.

**Neutral**
- A future Identity package version may add columns to `IdentityUser`. They have to be added to the module's configuration and to a migration by hand.
- Moving to Option 2 later would be a code-only change, because the table shape is the same.

## Links

- Spec: [[../spec.md]] AC-02, AC-03, AC-03b, AC-06, AC-13b, §6.1
- SAD: [[../sad.md]] §4 (choice 1), §5, §8
- Related ADR: [[0002-validate-cookie-security-stamp-on-every-request]], [[0003-store-one-role-per-account-in-a-column]]; repo [[../../../adr/0002-use-modular-monolith-with-feature-folders]], [[../../../adr/0003-use-ef-core-single-dbcontext-schema-per-module]]
