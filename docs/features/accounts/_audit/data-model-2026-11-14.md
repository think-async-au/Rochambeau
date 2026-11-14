# Audit — data-model · accounts · 2026-11-14

**Migrations are staged. They are not yet in the live `src/Rochambeau.Api/Common/Persistence/Migrations/` tree; `implement` promotes them.**

## Staged migrations

| Ordinal | Files | What |
|---|---|---|
| 01 | `docs/features/accounts/migrations/01_create_accounts.up.sql` · `.down.sql` | Creates the `identity` schema, the `identity.accounts` table, `ck_accounts_role` and two unique indexes |

## Promote-time convention hint

- The tool is **EF Core migrations** (`dotnet-ef`), not raw SQL files (architecture map · Migrations; repo ADR-0003). The live tree holds only `20261114090945_InitialCreate` (empty `Up`/`Down`).
- At promotion, `implement` writes the `IEntityTypeConfiguration<Account>` (`ToTable("accounts", "identity")`, the lengths, `IsRequired`, the two unique indexes, `HasCheckConstraint("ck_accounts_role", …)`, `Ignore(PhoneNumber, PhoneNumberConfirmed)`, the `role` converter). It then runs `dotnet ef migrations add AddIdentityAccounts --project src/Rochambeau.Api --output-dir Common/Persistence/Migrations`. EF assigns the timestamp, `<yyyyMMddHHmmss>_AddIdentityAccounts`, at that moment.
- **Equivalence check:** `dotnet ef migrations script InitialCreate AddIdentityAccounts` must match the staged `.up.sql` (EF's `DO $EF$` schema wrapper is fine).
- **Hand edit required:** add `migrationBuilder.DropSchema("identity")` to the generated `Down`. EF does not drop schemas, and without it the rollback is incomplete.
- Index and constraint names are the `EFCore.NamingConventions` defaults (`pk_accounts`, `ix_accounts_normalized_user_name`, `ix_accounts_normalized_email`). Application code maps `23505` by these names, so do not rename them.

## Conventions followed / decisions confirmed with the owner

| Topic | Decision | Source |
|---|---|---|
| Naming | snake_case, schema `identity` | repo ADR-0003, architecture map |
| Table name | `accounts` (plural). It sets the precedent: there was no earlier table to follow | here; first table in the repo |
| PK | `uuid`, UUIDv7 generated in the application | `CLAUDE.md` · IDs, accounts ADR-0001 |
| Audit columns | `created_at` only (the join date), from `TimeProvider`, no DB default | owner confirmed 2026-11-14 |
| Delete strategy | none in this step | no AC deletes an Account |
| CHECK constraints | **`ck_accounts_role` only.** This sets the repo's first CHECK precedent: a backstop on the escalation column. The username format stays a Domain rule | owner confirmed 2026-11-14 |
| Unused Identity columns | `phone_number`, `phone_number_confirmed` are ignored (data minimisation, spec §6.1). `email_confirmed` and `two_factor_enabled` are kept because `SignInManager` reads them | owner confirmed 2026-11-14 |
| Nullability | Identity's `string?` columns that the feature always fills are NOT NULL: username, email, normalized forms, `password_hash`, both stamps | here: every Account is created through register with a password |

## Deviations (architecture vs repo, or from framework defaults)

- **Staged format vs tool.** The pipeline stages `.up.sql`/`.down.sql`, but the repo's tool generates C# migrations. The SQL is the reference DDL, not a file to run. See the promote-time hint.
- **`normalized_email` unique.** This differs from the Identity default (non-unique), as accounts ADR-0001 decided.
- **`user_name` / `normalized_user_name` are `varchar(20)`, not Identity's 256.** They are sized from AC-02. If the username rule ever widens, that needs an expand migration.

## Drift detection

N/A. No Identity module or domain layer exists yet (`src/Rochambeau.Api/Modules/` has no `Identity/`), so there is no struct to compare with DDL. No `_drift/` output.

## Breaking-change decompositions

None. Every object is new, so no expand → backfill → contract is needed.

## Self-check (4 mandatory)

| Check | Result |
|---|---|
| Naming matches the repo convention | ✅ snake_case, `identity` schema, EF NamingConventions index and constraint names |
| Down reversibility | ✅ every CREATE in `.up` has a DROP in `.down` (2 indexes, table, schema) |
| FK indexes | ✅ there are no FKs. Every index is justified by a query (data-model.md · Indexes) |
| Convention adherence | ✅ deliberate divergences are listed above, none silent |
| Mermaid `erDiagram` | ✅ structural lint (`mmdc` is not installed): valid entity block, `type name [PK\|UK]` lines, no relationships |
| PII in seeds | ✅ no seeds. Fixtures use `@example.test` |

**Not verified:** the staged SQL was **not** executed against PostgreSQL, because Docker was not reachable from this session. `implement`'s integration tests (Testcontainers) and the `migrations script` equivalence check cover it.

## Open TBDs

- **Email maximum length.** AC-02 sets none, but the column is `varchar(256)`. Domain must reject longer input, or 254 (the RFC 5321 limit), otherwise an overlong email gives a 500 instead of a field error. → `clarify accounts`, or a Domain fix in `implement`.

## Next stage

`/sdd:api accounts`. The feature adds new endpoints (register, log in, log out, own Profile, public Profile), so the API stage is not skippable.
