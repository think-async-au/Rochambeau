---
status: Accepted
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
ticket: "docs/features/accounts/spec.md"
---

# 0004 — Expose the acting account through Identity Contracts and revalidate the session on every hub invocation

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), during the `design` Socratic walk for accounts

## Context

Every later module needs to know who is acting. Play (roadmap step 3) attributes moves to a player and looks up a challenged opponent by Username. Chat (step 5) attributes messages. Admin (step 6) checks the Administrator role and bans an Account. AC-08 says Accounts answers with the account identity, Username and role, or «nobody». AC-13b says that after a role change no device keeps acting with the old role, and that a ban must behave the same way. HTTP requests are revalidated on every request (accounts ADR-0002), but a SignalR WebSocket connection is authenticated only once, when it opens. This is a public contract used by every future module (SAD §5).

## Decision drivers

- Module boundaries: other modules call Identity only through its `Contracts/` namespace (repo ADR-0002, `CLAUDE.md` · Conventions).
- AC-08: one answer, «identity + Username + role» or «nobody», for every area of the portal.
- AC-13b: no device keeps acting with the old role, including over a long-lived real-time connection.
- Spec §7 «Downstream readiness»: steps 3 and 6 can be specified using only this feature's public identity.
- The ADR-0004 seam: authentication must later be replaceable (token login, 2FA, extraction into an Auth service) without touching the modules that use it (`target-architecture.md`).

## Considered options

1. **A Contracts interface plus a global hub filter.** `ICurrentAccount` returns `AccountIdentity(AccountId, Username, Role)` or «nobody» for the current request or hub invocation. `IAccountDirectory` looks Accounts up by id or exact Username. Claim names stay private. Identity registers a global `IHubFilter` that re-checks the security stamp on every hub method invocation and aborts the connection if the session was revoked.
2. **Shared claim-type constants.** Identity publishes only the claim names in `Contracts/`. Each module reads `HttpContext.User` (or the hub's `Context.User`) itself, and each hub re-checks revocation on its own.

## Decision outcome

**Chosen:** Option 1. Modules depend on a stable, typed answer («who is acting») instead of the claim layout. A token scheme, 2FA or an extracted Auth service changes only Identity's implementation. The hub filter enforces AC-13b once, for every hub, so no future module can forget it. Option 2 needs less code, but it couples every module to the cookie's claim format and leaves AC-13b to each hub's discipline.

Implementation notes that follow from this choice:
- `AccountId` is the account's UUIDv7 key. Other modules store it as their reference to a person, never the Username or email (`target-architecture.md` · «Player data is keyed by account identity»).
- `AccountIdentity` has no email field. The only way to read an email is the owner's own-profile query inside Identity (AC-09, AC-11).
- Opening a real-time connection counts as activity (AC-08): the negotiate request goes through the cookie middleware like any other request and renews the sliding cookie.
- The hub filter does one primary-key read per hub invocation. Any cache for it needs its own ADR (`CLAUDE.md` · Infrastructure).

## Consequences

**Positive**
- Steps 3, 5 and 6 code against `ICurrentAccount` / `IAccountDirectory` only. Identity can change its internals freely.
- AC-13b holds for HTTP (accounts ADR-0002) and for every hub (this filter) with no per-module work. A later ban reuses the same path.
- Tests in other modules can fake `ICurrentAccount` without building cookies.

**Negative**
- One database read per hub invocation (every move, every chat message), on top of the per-request read for HTTP.
- An extra layer (an interface and its implementation) compared with reading claims directly.

- An open connection's 7-day idle deadline is its own. It is set when the connection opens (from the renewed cookie) and moves only on hub invocations, not on the Player's HTTP activity elsewhere. A connection that only receives pushes for 7 days is refused on its next call even if the Player stayed active over HTTP. Accepted in review (`_review/review-2026-11-14-2.md`, R2) in place of shared per-account activity state. The client side covers it: every SignalR hook in `web/src/shared/realtime/` builds its connection with `withAutomaticReconnect`, so the refusal reconnects with the still-valid cookie, and that renews both the cookie and the deadline.

**Neutral**
- If the per-invocation read becomes a measured bottleneck (roadmap step 15), a short-lived revocation cache or a push-based disconnect can replace it behind the same contract, under its own ADR.

## Links

- Spec: [[../spec.md]] AC-08, AC-11, AC-12, AC-13b, §7 «Downstream readiness»
- SAD: [[../sad.md]] §4 (choice 4), §5, §6
- Related ADR: [[0002-validate-cookie-security-stamp-on-every-request]], [[0003-store-one-role-per-account-in-a-column]]; repo [[../../../adr/0004-evolve-monolith-toward-target-service-architecture]]
