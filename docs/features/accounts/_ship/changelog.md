# Changelog — accounts

## accounts — visitors can register, log in, see their Profile and look up other Players

**What:** A Visitor can now register with a username, an email address and a password, and is logged in straight away. A Player can log in, log out, see their own Profile (username, email, join date, role) and open another Player's public Profile (username, join date and role, never the email) by exact username. The Administrator role exists from day one. Only the operator can grant or remove it, from the host command line, and doing so ends every session of that Account. Other modules (Play, Chat, Admin, and their SignalR hubs) can ask Identity who is acting through `ICurrentAccount` / `IAccountDirectory`. They never read the Identity tables.

**Why:** Every later roadmap step (a match against a chosen opponent, chat, moderation) needs to know *who* is acting. Until now nobody could be identified ([spec](../spec.md) §1, §2; `docs/roadmap.md` step 2). Key decisions:
- [ADR-0001](../adr/0001-use-identity-core-with-module-owned-mappings.md): ASP.NET Core Identity core, with the entity mappings owned by the Identity module in schema `identity`.
- [ADR-0002](../adr/0002-validate-cookie-security-stamp-on-every-request.md): an encrypted sliding cookie (7 days idle, renewed after 3.5 days), with the security stamp checked on every request, so a role change ends sessions at once.
- [ADR-0003](../adr/0003-store-one-role-per-account-in-a-column.md): exactly one role per Account, stored in a column.
- [ADR-0004](../adr/0004-expose-acting-account-through-identity-contracts.md): the acting account is exposed through Identity Contracts, and the session is revalidated on every hub invocation.
- [ADR-0005](../adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md): CSRF defence with a `SameSite=Strict` cookie plus a required `X-Rochambeau-Client` header.

**How to use:** In the SPA, go to `/register` or `/login` from the landing page. For the API, see [openapi.yaml](../contracts/openapi.yaml). Every unsafe request and the hub negotiate must send `X-Rochambeau-Client`.

```http
POST /api/v1/accounts        {"username":"Rocky","email":"rocky@example.com","password":"correct-horse"} → 201 + session cookie
POST /api/v1/session         {"email":"rocky@example.com","password":"correct-horse"}                    → 200 | 401 | 423
GET  /api/v1/me                                                                                           → 200 own Profile | 401
GET  /api/v1/profiles/rocky                                                                               → 200 public Profile | 404 | 401
DELETE /api/v1/session                                                                                    → 204 (this browser only)
```

Operator: `dotnet run --project src/Rochambeau.Api -- grant-admin <username>` and `... -- revoke-admin <username>`. The command runs and exits, and it ends all sessions of that Account.

**Operational notes:**
- Migration: adds `20261114133227_AddIdentityAccounts`, which creates schema `identity` and table `identity.accounts` with case-insensitive unique indexes on username and email. Run `dotnet ef database update --project src/Rochambeau.Api` on deploy (only Development auto-migrates). It reverts cleanly with `dotnet ef database update 20261114090945_InitialCreate`. The `Down` drops the table, then drops the schema with RESTRICT.
- Config (required): `DataProtection:KeysPath` must be set in every environment, and the host refuses to start without it. Only `appsettings.Development.json` sets it, to `App_Data/keys`. The session cookie is encrypted with the key ring stored there. On a deploy, point it at a persistent directory that all instances share, or every redeploy logs everyone out.
- Registration cap (AC-15): an in-process sliding window, 5 successful registrations per network address per 60 minutes. It is per instance and resets on restart. Behind a proxy it counts the proxy's address until spec §8 OQ (forwarded address / IPv6 /64) is decided.
- Feature flag: none.
- Rollback: revert the deploy, then apply the down migration above. Revoking leaves no other data behind.

**Acceptance criteria delivered:** AC-01 … AC-15, including AC-03b and AC-13b. Registration with field-level validation, case-insensitive uniqueness that holds under concurrent registration, login with enumeration-safe errors, a 5-strikes / 15-minute lockout, logout from this browser only, a 7-day idle session, own and public Profiles with the email kept private, an operator-only Administrator role whose changes end every session, and a per-address registration cap.

## Verification at ship (2026-11-15)

- Gate: `dotnet build` passed with 0 warnings. Unit 100/100, integration 115/115 (Testcontainers PostgreSQL, Docker Desktop). `dotnet format --verify-no-changes` is clean. Web lint is clean, web tests pass 42/42 and `vite build` succeeds.
- Run against the live API (Development, local PostgreSQL 17):
  - **AC-01 / AC-14:** registering with `"role":"administrator"` in the body returned `201` with `role: player`, and the cookie worked on `GET /me` straight away.
  - **AC-02:** too short a username, a malformed email and a 5-character password returned `400 identity.validation_failed` with one message per field.
  - **AC-03:** the same username in upper case returned `409 identity.username_taken`.
  - **AC-04 / AC-05:** login with `"  SHIP…@EXAMPLE.COM "` returned `200`. A wrong password and an unknown email both returned `401`, "Email or password is incorrect."
  - **AC-06:** the 5th wrong password returned `423`, and so did the correct password right after it, with `lockoutEnd` 15 minutes ahead.
  - **AC-07:** after `DELETE /session` on browser A, `/me` on A returned `401`, while browser B, logged in separately, still got `200`.
  - **AC-10 / AC-11 / AC-12:** a Visitor opening a Profile got `401`. A Player opening the Profile by the lower-case username got username, join date and role with no email. An unknown username got `404`, "No player with that username exists."
  - **AC-13 / AC-13b:** `grant-admin` turned the old session's `/me` into `401`, and re-login showed `role: administrator`. `revoke-admin` ended that session too.
  - **ADR-0005:** an unsafe request without `X-Rochambeau-Client` got `400`.
- Covered by integration tests only, not run by hand: AC-03b (parallel registrations), AC-08 (controlled-clock 3.5-day / 7-day idle bounds, and hub activity), and AC-15 (running it by hand would block local registration for an hour).
- Deferred: the §7 KPI "first visit → logged-in Profile ≤ 60 s, 1 submission" needs a manual click-through of the SPA in a browser (spec §8 OQ, e2e runner). It was not done in this session.
