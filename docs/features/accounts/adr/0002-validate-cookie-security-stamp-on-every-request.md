---
status: Accepted
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
ticket: "docs/features/accounts/spec.md"
---

# 0002 — Keep sessions in an encrypted sliding cookie and validate the security stamp on every request

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), during the `design` Socratic walk for accounts

## Context

Once a Player logs in, every later request in every module has to know who is acting (AC-08). The spec asks for four things. Logout ends the session in this browser only, and several devices may be logged in at once (AC-07). The Player is logged out no earlier than 3.5 and no later than 7 days after their last activity (AC-08, spec §6 «Session lifetime»). A role change by the operator ends the sessions on every device, and until then no device keeps acting with the old role (AC-13b). A later ban must work the same way (AC-13b, roadmap step 6). How the session is held shapes every request and the later token login and 2FA (SAD §4).

## Decision drivers

- AC-13b: a role change must apply on every device before its next action.
- AC-08 / spec §6: an idle logout window of 3.5–7 days.
- Spec §6.1 accepts as residual risk that a cookie copied before logout stays valid until it expires.
- Spec §6 latency: log in p95 ≤ 400 ms, view a profile p95 ≤ 150 ms.
- Solo capacity: no new infrastructure without an ADR (`CLAUDE.md` · Conventions).

## Considered options

1. **Encrypted cookie + security-stamp check on every request.** The Identity application cookie carries the claims ticket itself. `ExpireTimeSpan = 7 days` with `SlidingExpiration`. `SecurityStampValidator` runs with `ValidationInterval = TimeSpan.Zero`, so each authenticated request reads the account row by primary key, rejects the cookie if the stamp changed, and otherwise re-issues it. Because the cookie is renewed on every request, the idle logout lands exactly 7 days after the last activity.
2. **Server-side sessions.** The cookie carries only a session id, and the ticket is stored in an `identity.sessions` table through `ITicketStore`. Logout deletes the row, which kills a copied cookie at once. A role change deletes all of the account's rows. This needs a cleanup job for expired rows.

## Decision outcome

**Chosen:** Option 1. It meets AC-07, AC-08 and AC-13b with framework mechanisms only: no new table and no cleanup job. The one thing it gives up (a copied cookie outliving logout) is a residual risk the spec already accepted (§6.1). Both options cost one database read per authenticated request. Option 2 stays the upgrade path if «log out everywhere» or a session list is ever needed.

Implementation notes that follow from this choice:
- A role change (accounts ADR-0003) and a later ban go through `UserManager.UpdateSecurityStampAsync`, which invalidates every device's cookie on its next request.
- When validation succeeds, the validator re-issues the principal, so the role claim is always rebuilt from the database and never trusted from an old cookie.
- Data Protection keys (which encrypt the cookie) are persisted, so a restart does not log everyone out (SAD §8).
- Cookie: `HttpOnly`, `Secure`, `SameSite=Strict` (SAD §8). An unauthenticated API call returns 401 and a forbidden one returns 403, instead of the default redirect to a login page.

## Consequences

**Positive**
- A role change takes effect on every device's next request (AC-13b). Idle expiry lands exactly 7 days after the last activity, inside the 3.5–7 day window (AC-08). Logout is per browser (AC-07).
- No session table, no cleanup job, and no new infrastructure.
- A token scheme for non-browser clients can later be added next to the cookie scheme without changing modules that use the identity contract (spec §3).

**Negative**
- One primary-key read of the account row on every authenticated request, plus a `Set-Cookie` header on every response, because the validator re-issues the principal.
- A cookie copied before logout stays valid for up to 7 idle days (accepted residual risk, spec §6.1).
- A long-lived real-time connection is authenticated once, at connect. AC-13b therefore needs an explicit per-invocation check on hubs (SAD §5, accounts ADR-0004).

**Neutral**
- Switching to server-side sessions (Option 2) later changes only cookie configuration and adds one table. Modules that use the identity contract are unaffected.

## Links

- Spec: [[../spec.md]] AC-07, AC-08, AC-13b, §6 «Session lifetime», §6.1 «Stolen session after logout»
- SAD: [[../sad.md]] §4 (choice 2), §5, §8
- Related ADR: [[0001-use-identity-core-with-module-owned-mappings]], [[0003-store-one-role-per-account-in-a-column]]
