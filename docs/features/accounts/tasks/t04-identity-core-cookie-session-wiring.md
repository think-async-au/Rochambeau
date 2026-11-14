---
id: T4
title: "Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule"
layer: "wiring"
deps: ["T1", "T3"]
blocks: ["T5"]
acs: ["AC-08"]
files_hint: ["src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "src/Rochambeau.Api/Modules/Identity/Infrastructure/", "src/Rochambeau.Api/Program.cs", "src/Rochambeau.Api/Rochambeau.Api.csproj", "src/Rochambeau.Api/appsettings.json", "tests/Rochambeau.IntegrationTests/Infrastructure/", "tests/Rochambeau.IntegrationTests/Identity/"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 48 inlined lines
status: "done"
step: "COMMIT"
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T4 — Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule

## Place in the sequence

- **Blocked by:** T1 — Add Result/Error, the ProblemDetails mapping and the client-header filter to Common · T3 — Map the Account entity to identity.accounts and promote the staged migration
- **Blocks:** T5 — Expose ICurrentAccount and IAccountDirectory through Identity Contracts
- **Wave:** 3 — starts once T1, T3 are done.
- **Lane:** shares `Program.cs` with T1/T12 and `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T5–T13 — serialized.

## Why (user story)

> **As a** Visitor who already has an Account
> **I want** to log in with my email address and password
> **So that** the portal recognises me as my Player again
>
> — `spec.md §4, US-02, verbatim` · full text: [spec.md](../spec.md)

This task sets up the session mechanism behind "the portal recognises me again" and the 3.5–7-day idle window of AC-08. Endpoints come later.

## Inlined context

> 2. **Keep sessions in an encrypted cookie and validate the security stamp on every request** → [accounts ADR-0002](adr/0002-validate-cookie-security-stamp-on-every-request.md). The Identity cookie has a 7-day sliding lifetime. `SecurityStampValidator` runs with a zero interval: on every authenticated request it re-checks the stamp and re-issues the cookie. The idle logout therefore lands exactly 7 days after the last activity, inside the spec's «no earlier than 3.5, no later than 7 days» window (AC-08). A role change rotates the stamp and invalidates every device's cookie on its next request (AC-13b). Logout clears only this browser's cookie (AC-07). No session table is added (quality goals 1, 3).
>
> — `sad.md §4, strategic choice 2, verbatim` · full text: [sad.md](../sad.md)

> **Authentication:** ASP.NET Core Identity application cookie: `HttpOnly`, `Secure`, `SameSite=Strict`, 7-day sliding lifetime, security stamp validated on every request. Unauthenticated API calls get 401 and forbidden ones get 403, with no redirect to a login page. Every module reads the acting person only through `ICurrentAccount`
>
> — `sad.md §8, Authentication row, verbatim` · full text: [sad.md](../sad.md)

> **Hard rule:** **Authorization:** A **deny-by-default** fallback policy (authenticated user required) for every endpoint in every module. Only register, log in, `/health`, OpenAPI (Development) and the SPA's static files opt out with `AllowAnonymous`. Role checks use the standard role claim (`Administrator`)
>
> — `sad.md §8, Authorization row, verbatim` · full text: [sad.md](../sad.md)

> **Password storage:** Identity's default `PasswordHasher` (PBKDF2, a deliberately slow one-way function). The only password rule is length 8–128 (AC-02); Identity's digit/case/symbol requirements are switched off
>
> **Lockout:** Identity lockout: 5 consecutive failures → locked for 15 minutes. The count resets when the lock starts and on a successful login; attempts during the lock neither count nor extend it (AC-06)
>
> — `sad.md §8, Password storage + Lockout rows, verbatim` · full text: [sad.md](../sad.md)

> **Data Protection:** The key ring is persisted to a configured directory on the host (`DataProtection:KeysPath`), so cookies survive a restart. A shared key store is required before a second instance (§7, §11)
>
> **Time:** A single `TimeProvider` registration, read by the application layer, Identity lockout and cookie expiry. Integration tests swap in a fake clock for the session-lifetime and lockout checks (spec §6)
>
> — `sad.md §8, Data Protection + Time rows, verbatim` · full text: [sad.md](../sad.md)

> - When validation succeeds, the validator re-issues the principal, so the role claim is always rebuilt from the database and never trusted from an old cookie.
> - Cookie: `HttpOnly`, `Secure`, `SameSite=Strict` (SAD §8). An unauthenticated API call returns 401 and a forbidden one returns 403, instead of the default redirect to a login page.
>
> — `adr/0002 §Decision outcome, implementation notes 2 + 4, verbatim` · full text: [adr/0002-validate-cookie-security-stamp-on-every-request.md](../adr/0002-validate-cookie-security-stamp-on-every-request.md)

> The application never does a "check, then insert"; the unique indexes are the race guard (sad §6). So keep Identity's `RequireUniqueEmail` off, and let `AllowedUserNameCharacters` admit the Domain alphabet.
>
> — `sad.md §6, critical flow 1 note, abridged` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- Security scheme `CookieSession`: cookie `.AspNetCore.Identity.Application`, `HttpOnly`, `Secure`, `SameSite=Strict`, 7-day sliding lifetime, renewed on every authenticated request. Same-origin only, no CORS.
- `NotAuthenticated` response: `401`, `errorCode` `identity.not_authenticated`, title "Log in or register first.". No profile details; a stale cookie is cleared. Never a redirect.

— `contracts/openapi.yaml §components, securitySchemes.CookieSession + responses.NotAuthenticated, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-08 — cross-context

> **Given** a Player who is logged in, and another area of the portal (such as play, chat or moderation) that needs to know who is acting
> **When** that area asks Accounts who is behind the current request
> **Then** Accounts answers with that Player's account identity, username and role, without the Player logging in again. It answers "nobody" for a Visitor, after the Player logs out on that device, after the operator changes the Account's role (AC-13b), or after 7 days without activity. Activity is any request the logged-in Player makes to the portal, including opening a real-time connection. The Player is logged out no earlier than 3.5 days and no later than 7 days after their last activity
>
> — `spec.md §5, AC-08, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `AddIdentityModule(IServiceCollection, IConfiguration)` / `MapIdentityModule(...)` skeleton — `IdentityModule.cs`
- [ ] `AddIdentityCore<Account>().AddSignInManager().AddEntityFrameworkStores<AppDbContext>()` with users only (no role store). Add the Identity EF package if it is not referenced — `IdentityModule.cs`, `Rochambeau.Api.csproj`
- [ ] Identity options: password `RequiredLength = 8` with digit/upper/lower/non-alphanumeric off; lockout `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`; `RequireUniqueEmail = false` — `IdentityModule.cs`
- [ ] Application cookie: `HttpOnly`, `SecurePolicy = Always`, `SameSite = Strict`, `ExpireTimeSpan = 7 days`, `SlidingExpiration = true`. `OnRedirectToLogin` → `401` ProblemDetails `identity.not_authenticated`; `OnRedirectToAccessDenied` → `403`. `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` — `Infrastructure/`
- [ ] Make Identity and the cookie read the single `TimeProvider` registration (sad §8 Time) — `IdentityModule.cs`
- [ ] Fallback authorization policy: authenticated user required. `/health` and OpenAPI (Development) are `AllowAnonymous` — `IdentityModule.cs`, `Program.cs`
- [ ] Data Protection: `PersistKeysToFileSystem(DataProtection:KeysPath)`, with a development default in `appsettings` — `IdentityModule.cs`, `appsettings.json`
- [ ] `Program.cs`: `UseAuthentication()` / `UseAuthorization()` plus the module's Add/Map calls only — `Program.cs`
- [ ] `ApiFactory`: a temp `DataProtection:KeysPath`, an https client base address (so the `Secure` cookie round-trips), and a hook to swap in a fake `TimeProvider` for later tasks — `tests/Rochambeau.IntegrationTests/Infrastructure/`

## Edge cases

| Case | Behaviour |
|---|---|
| Anonymous request to any endpoint without `AllowAnonymous` | `401` ProblemDetails `identity.not_authenticated`, no `Location` redirect |
| Authenticated but forbidden | `403`, no redirect |
| `GET /health` anonymously | `200` (the existing `SmokeTests` stay green) |
| Restart with the same `KeysPath` | Existing cookies remain valid (sad §8 Data Protection) |

## Definition of Done

- [ ] Integration test: a probe endpoint registered only in the test host (e.g. via `IStartupFilter` in `WithWebHostBuilder`) answers anonymous calls with `401 identity.not_authenticated` and no redirect
- [ ] Integration test resolves `IOptions<…>` from the host and asserts the cookie (Strict, Secure, HttpOnly, 7 d, sliding), stamp interval (zero), lockout (5 / 15 min) and password (8, no classes) values
- [ ] Existing `SmokeTests` (`GET /health` → 200) still pass
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
