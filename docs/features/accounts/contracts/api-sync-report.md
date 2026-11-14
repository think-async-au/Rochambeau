---
status: Draft
owner: "a.udalov"
updated_at: "2026-11-14"
feature_size: "M"
---

# API sync report — accounts

> **Contract:** [openapi.yaml](openapi.yaml) (OpenAPI 3.1.0, `info.version` 0.1.0)
> **Derived from:** [data-model.md](../data-model.md) (`identity.accounts`) · [sad.md](../sad.md) §6 (9 flows), §8 · [spec.md](../spec.md) §4 US-01–US-06, §5 AC-01–AC-15 · accounts ADR [0001](../adr/0001-use-identity-core-with-module-owned-mappings.md)–[0005](../adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md) · `CLAUDE.md` · Conventions.
> **Interface kind:** `backend-service` → HTTP/REST (from `sad.md` `target_surfaces: [backend-service, web-frontend]`; `web-frontend` consumes this contract). No async flows → no `events.md` (SAD §6: «All are synchronous», §8 «Events: None»).
> **Size / route:** M / standard (from `.size` / `.route`).

## Operations

| operationId | Method + path | User story | ACs |
|---|---|---|---|
| `registerAccount` | `POST /api/v1/accounts` | US-01 | AC-01, AC-02, AC-03, AC-03b, AC-14, AC-15 |
| `logIn` | `POST /api/v1/session` | US-02, US-06 | AC-04, AC-05, AC-06, AC-13 |
| `logOut` | `DELETE /api/v1/session` | US-03 | AC-07 |
| `getOwnProfile` | `GET /api/v1/me` | US-04, US-06 | AC-08, AC-09, AC-10, AC-13, AC-13b |
| `getPublicProfile` | `GET /api/v1/profiles/{username}` | US-05 | AC-10, AC-11, AC-12 |

## Deviations from the api-skill defaults

| Default | This contract | Mandated by |
|---|---|---|
| `BearerAuth` global | `CookieSession` (apiKey in cookie `.AspNetCore.Identity.Application`) global; public ops declare `security: []` | `CLAUDE.md` · Auth; accounts ADR-0002 |
| Error envelope `{code, message, details?}` | RFC 9457 ProblemDetails (`application/problem+json`) with `errorCode` (`module.error_name`); field errors in `errors` keyed by field; lockout adds `lockoutEnd` | `CLAUDE.md` · Errors; SAD §8 «Error handling» |
| — | `X-Rochambeau-Client` header required on every unsafe op | accounts ADR-0005 |
| Request bodies closed (`additionalProperties: false`) | Request bodies open — unknown fields (e.g. `role`) are ignored, never rejected | SAD §6 «Self-escalation attempt» (bind only known fields), AC-14 |
| Cursor pagination | Not used — no list endpoint (spec §3: no player list or search) | spec §3 |
| JSON field naming | camelCase (`joinedAt`, `lockoutEnd`) — ASP.NET Core `System.Text.Json` web default | repo stack (repo ADR-0001) |

## A. Field origins

| schema_path | origin | confidence |
|---|---|---|
| registerAccount.username | data-model.md → accounts.user_name varchar(20) + spec AC-02 (3–20, `[A-Za-z0-9_-]`, trimmed) | high |
| registerAccount.email | data-model.md → accounts.email varchar(256); maxLength 254 per F-3 | high |
| registerAccount.password | spec AC-02 (8–128); stored only as accounts.password_hash | high |
| registerAccount.201.* | → OwnProfile | high |
| registerAccount.201.Location | derived (REST convention, own-Profile resource) | medium |
| logIn.email | data-model.md → accounts.email / normalized_email (lookup key, `ix_accounts_normalized_email`) | high |
| logIn.password | verified against accounts.password_hash (spec AC-04) | high |
| logIn.200.* | → OwnProfile (SAD §6 log-in flow: UI shows own Profile) | medium |
| logIn.423.lockoutEnd | data-model.md → accounts.lockout_end timestamptz | high |
| logIn.423.Retry-After | derived from accounts.lockout_end − now (AC-06 «when it can be tried again») | high |
| getPublicProfile.username (path) | data-model.md → accounts.normalized_user_name (`ix_accounts_normalized_user_name`) | high |
| OwnProfile.username | data-model.md → accounts.user_name | high |
| OwnProfile.email | data-model.md → accounts.email | high |
| OwnProfile.joinedAt | data-model.md → accounts.created_at timestamptz | high |
| OwnProfile.role | data-model.md → accounts.role, CHECK `ck_accounts_role` (`player`, `administrator`) | high |
| PublicProfile.username | data-model.md → accounts.user_name | high |
| PublicProfile.joinedAt | data-model.md → accounts.created_at | high |
| PublicProfile.role | data-model.md → accounts.role | high |
| ProblemDetails.errorCode | SAD §8 «Error handling» + accounts ADR-0005 (see point 2) | high / medium |
| ValidationProblemDetails.errors | SAD §8 «field-level validation failures … ProblemDetails `errors` keyed by field» | high |
| CookieSession.name | ASP.NET Core Identity default cookie name; SAD does not name the cookie | medium |

**Model columns deliberately not exposed** (internal to Identity, no AC reads them): `id`, `normalized_user_name`, `normalized_email`, `email_confirmed`, `password_hash`, `security_stamp`, `concurrency_stamp`, `two_factor_enabled`, `lockout_enabled`, `access_failed_count`. The account `id` reaches other modules only through `Modules/Identity/Contracts/` (accounts ADR-0004), not HTTP.

## B. Drift checklist

1. **Endpoint ↔ data-model** *(core)* — ✓. Every operation reads or writes `identity.accounts`: register inserts; log in reads by `normalized_email` and writes `access_failed_count` / `lockout_end`; log out reads the row via the per-request stamp check; both profile reads use the PK or `normalized_user_name`.
2. **Error code ↔ repo error definition** *(core)* — ✓ with note. **No error registry found** — `src/Rochambeau.Api/Common/` has only `Persistence/`; `Result<T>` / `Error` arrive with this feature (SAD §5, §11). Codes are the contract's proposal; reconcile when `Common/Results/` and the Identity error definitions exist.
   | errorCode | Status | Source |
   |---|---|---|
   | `common.client_header_required` | 400 | accounts ADR-0005 (named there) |
   | `identity.validation_failed` | 400 | SAD §8 (field errors; code name proposed here) |
   | `identity.username_taken` | 409 | SAD §8, data-model.md unique-violation mapping |
   | `identity.email_unavailable` | 409 | data-model.md «email cannot be used» error (code name proposed here) |
   | `identity.registration_cap` | 429 | SAD §8 |
   | `identity.invalid_credentials` | 401 | SAD §8 |
   | `identity.locked_out` | 423 | SAD §8 |
   | `identity.not_authenticated` | 401 | SAD §8 «Unauthenticated API calls get 401» (code name proposed here) |
   | `identity.player_not_found` | 404 | AC-12 (code name proposed here) |
3. **Validation ↔ constraint** *(core)* — ✓. Username `minLength 3`, `maxLength 20`, `pattern ^[A-Za-z0-9_-]{3,20}$` ↔ `user_name varchar(20)` + AC-02. Role `enum [player, administrator]` ↔ `ck_accounts_role`. Password 8–128 ↔ AC-02 (no column; hashed). Email `maxLength 254` — stricter than `varchar(256)`, resolved as F-3.
4. **OpenAPI ↔ sequence** *(supporting)* — ✓ with F-2 accepted. Branch-to-response map:
   | §6 flow → alt-branch | Response |
   |---|---|
   | Register → a field breaks a rule | `registerAccount` 400 `identity.validation_failed` |
   | Register → no free cap slot | `registerAccount` 429 `identity.registration_cap` |
   | Register → unique violation (username / email) | `registerAccount` 409 `identity.username_taken` / `identity.email_unavailable` |
   | Register → inserted | `registerAccount` 201 + Set-Cookie + OwnProfile |
   | Log in → no Account holds this email | `logIn` 401 `identity.invalid_credentials` |
   | Log in → locked, 15 min not passed | `logIn` 423 `identity.locked_out` |
   | Log in → wrong password, 5th failure | `logIn` 423 `identity.locked_out` |
   | Log in → wrong password, < 5 failures | `logIn` 401 `identity.invalid_credentials` |
   | Log in → password correct | `logIn` 200 + Set-Cookie |
   | Log out → session ended | `logOut` 204 + cookie cleared |
   | Log out → back to Profile without a session | `getOwnProfile` 401 |
   | Own Profile → nobody / logged-in Player | `getOwnProfile` 401 / 200 |
   | Public Profile → nobody / not found / found | `getPublicProfile` 401 / 404 / 200 |
   | Self-escalation → registration / other request | `registerAccount` binds no `role`; open request schemas ignore unknown fields |
   | Cross-cutting who-is-acting → no session / expired / revoked | `NotAuthenticated` 401 on every protected op |
   | Cross-cutting → stamp matches, renew 7 days | protected ops succeed; cookie re-issued (CookieSession description) |
   | Critical flow 2 → old cookie after role change | `NotAuthenticated` 401 |
   | Operator command (AC-13, AC-13b) | — not HTTP: host command (SAD §5); out of this contract by design |
   | Critical flow 2 / cross-cutting → hub call aborted | — not HTTP: SignalR hub filter + `ICurrentAccount` in-process contract (accounts ADR-0004) |

**Back-feed (coverage).** Every AC maps to ≥1 operation, except the parts that are not HTTP by design: AC-08 (the in-process answer to other modules — `ICurrentAccount`, ADR-0004), AC-13b (hub abort + operator command), AC-14 (no endpoint exists; the contract guarantees it by having no role-writing operation). Every operation maps to a §4 user story and ≥1 AC. No orphan sequence. `400 common.client_header_required` appears in no §6 flow; it comes from ADR-0005 as a crosscutting filter — accepted.

## Findings and resolutions

| # | Finding | Kind | Resolution |
|---|---|---|---|
| F-1 | Registering while the browser is already logged in: no AC (AC-01 covers only a Visitor) and no §6 branch. Contract default: the new session replaces the old one in this browser. | spec gap | **Save as OQ** → OQ-1 (owner `specify`); operation marked `# unresolved` |
| F-2 | `logIn` 400 for a missing/empty field has no branch in the §6 log-in flow. A malformed email is answered as `401 invalid_credentials` (AC-05). | sequence gap | **Accepted** — the general ProblemDetails field-error convention (SAD §8), not a new business branch |
| F-3 | Email max length undefined in AC-02; column is `varchar(256)` (carried TBD from data-model.md). | constraint | **Fixed in contract** — `maxLength: 254` (RFC 5321). `data-model.md` TBD and spec AC-02 should state the same limit (Domain email rule) — close in `clarify` or `implement` |

## Open questions

| # | Question | Owner | Due |
|---|---|---|---|
| OQ-1 | What happens when a logged-in Player calls register: replace this browser's session with the new Account's (current default), or refuse until they log out? Needs an AC in spec §5 and, if refused, a §6 branch + a `409` here. | `specify` (a.udalov) | before the contract is finalized (before `implement`) |

## Lint

`spectral lint docs/features/accounts/contracts/openapi.yaml` was not run in this session (not installed; running it via `npx` was not permitted). All `$ref` targets were checked locally. Wire Spectral into the Lint target when the repo adds an OpenAPI check.
