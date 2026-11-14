---
status: Accepted
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
ticket: "docs/features/accounts/spec.md"
---

# 0005 — Require a SameSite=Strict auth cookie and a custom client header against CSRF

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), during the `design` Socratic walk for accounts

## Context

Accounts authenticates with a cookie (accounts ADR-0002), and the browser attaches cookies to requests automatically. Without protection, a page on another site could make a logged-in Player's browser send a state-changing request to the portal: log out, and from step 3 on, make a move or send a chat message. This is cross-site request forgery (CSRF). The rule applies to every state-changing endpoint and to SignalR negotiation in every module, so it is fixed now, with the first cookie (SAD §8).

## Decision drivers

- Spec §6.1: a new authentication boundary, so a security review is required before shipping.
- Same-origin SPA (`CLAUDE.md` · Auth): the legitimate client always calls the API from the same site.
- The client-agnostic server constraint (spec §1, §3): future native clients must be able to call the API without browser-only machinery.
- Solo capacity: the least moving parts that still close the attack.

## Considered options

1. **`SameSite=Strict` cookie plus a required custom header.** The browser does not send the auth cookie on requests started by another site. A global endpoint filter also rejects every POST / PUT / PATCH / DELETE under `/api`, and the SignalR negotiate request, that lack an `X-Rochambeau-Client` header. A cross-site HTML form cannot set custom headers. A cross-origin `fetch` with one triggers a CORS preflight (the browser asking the server's permission first), which the API refuses because no CORS policy is configured.
2. **ASP.NET Core antiforgery tokens.** The server issues a per-user token (cookie `XSRF-TOKEN`), the SPA echoes it in an `X-XSRF-TOKEN` header, and an endpoint filter validates it through `IAntiforgery`. The token must be re-fetched after every log-in and log-out.

## Decision outcome

**Chosen:** Option 1. Two independent browser-enforced barriers (SameSite and the preflight on custom headers) close CSRF for a same-origin SPA with no server state, no token endpoint and no refetch-after-login logic. A native client only has to send one header. Option 2 is the framework's documented defence and does not depend on SameSite, but it adds a token lifecycle to the SPA and to every future client, and Minimal API JSON endpoints would still need a hand-written validation filter.

Implementation notes that follow from this choice:
- The SPA's shared fetch wrapper (`web/src/shared/api/`) always sends the header. The SignalR client sends it on negotiate through its `headers` option. The WebSocket upgrade itself is a GET and is not an unsafe method.
- A missing header is rejected before any handler runs, with a ProblemDetails 400 (`errorCode` `common.client_header_required`).
- Integration tests assert that an unsafe request without the header is rejected even with a valid cookie.

## Consequences

**Positive**
- Stateless, about one filter and one fetch-wrapper line. It applies to every module automatically.
- Future native clients (spec §3 «Token-based login») just send the header.

**Negative**
- It relies on modern-browser SameSite and CORS behaviour. Very old browsers lose one of the two barriers.
- `SameSite=Strict` means a top-level navigation arriving from another site carries no cookie. The SPA shell loads anonymously, then its same-site API calls carry the cookie as usual.

**Neutral**
- If CORS is ever opened for another origin, the header barrier weakens for that origin, and this ADR must be revisited (a §11 row).
- Switching to antiforgery tokens later is contained: the filter changes, and the fetch wrapper sends a token instead of a constant header.

## Links

- Spec: [[../spec.md]] §6.1 «Security review», §1 and §3 (client-agnostic server)
- SAD: [[../sad.md]] §8
- Related ADR: [[0002-validate-cookie-security-stamp-on-every-request]]
