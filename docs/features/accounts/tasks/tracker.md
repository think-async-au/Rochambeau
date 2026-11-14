# Tracker — accounts

> Status of every task in the epic. `implement` updates **Status** and **Step** after every
> subagent it runs (and commits them with the task), so this table shows where the run is right now.
> Status: `todo` · `in_progress` · `blocked` · `review` · `done`.
> Step: `SELECT` · `RED` · `GREEN` · `REFACTOR` · `GATE` · `REVIEW` · `COMMIT` (`—` before the task starts).
> A `blocked` row carries its reason in Step, e.g. `GATE — lint red after 3 retries`.

| # | Task | Layer | Owner | Estimate | Blocked by | Status | Step |
|---|---|---|---|---|---|---|---|
| T1 | [Add Result/Error, the ProblemDetails mapping and the client-header filter to Common](./t01-common-results-problemdetails-client-header.md) | ports | a.udalov | M | — | done | COMMIT |
| T2 | [Encode the Identity domain rules: username, email, password, role, lockout and cap policy](./t02-identity-domain-rules.md) | domain | a.udalov | S | — | done | COMMIT |
| T3 | [Map the Account entity to identity.accounts and promote the staged migration](./t03-account-entity-and-migration.md) | migration | a.udalov | M | T2 | done | COMMIT |
| T4 | [Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule](./t04-identity-core-cookie-session-wiring.md) | wiring | a.udalov | M | T1, T3 | done | COMMIT |
| T5 | [Expose ICurrentAccount and IAccountDirectory through Identity Contracts](./t05-identity-contracts-current-account.md) | infra | a.udalov | M | T4 | done | COMMIT |
| T6 | [Implement the in-process RegistrationCap sliding window](./t06-registration-cap.md) | app | a.udalov | S | T2 | done | COMMIT |
| T7 | [Register an Account through POST /api/v1/accounts](./t07-register-endpoint.md) | ports | a.udalov | M | T5 | done | COMMIT |
| T8 | [Enforce the registration cap and prove the race guard on registration](./t08-registration-cap-and-race-guard.md) | app | a.udalov | M | T6, T7 | done | COMMIT |
| T9 | [Log in through POST /api/v1/session with lockout and enumeration-safe errors](./t09-login-endpoint-lockout.md) | ports | a.udalov | M | T7 | done | COMMIT |
| T10 | [Serve GET /api/v1/me and log out with DELETE /api/v1/session](./t10-own-profile-and-logout.md) | ports | a.udalov | M | T9 | done | COMMIT |
| T11 | [Serve the public Profile through GET /api/v1/profiles/{username}](./t11-public-profile-endpoint.md) | ports | a.udalov | S | T7 | done | COMMIT |
| T12 | [Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow](./t12-operator-command-change-role.md) | app | a.udalov | M | T10 | done | COMMIT |
| T13 | [Revalidate the session on every hub invocation with SessionRevalidationHubFilter](./t13-session-revalidation-hub-filter.md) | infra | a.udalov | M | T12 | done | COMMIT |
| T14 | [Measure the spec §6 latency targets for register, log in and view a profile](./t14-latency-nfr-tests.md) | tests | a.udalov | S | T8, T9, T11 | done | COMMIT |
| T15 | [Add the shared API client and the «me» query to the SPA](./t15-web-api-client-and-me-query.md) | ui | a.udalov | S | — | done | COMMIT |
| T16 | [Build the Register screen with field-level errors](./t16-web-register-screen.md) | ui | a.udalov | M | T15 | done | COMMIT |
| T17 | [Build the Log-in screen, the log-out action and the logged-in route guard](./t17-web-login-logout-route-guard.md) | ui | a.udalov | M | T16 | done | COMMIT |
| T18 | [Build the own Profile and public Profile screens](./t18-web-profile-screens.md) | ui | a.udalov | M | T17 | done | COMMIT |
| T19 | [Resolve ICurrentAccount from the hub caller inside hub invocations](./t19-hub-scoped-current-account.md) | infra | a.udalov | S | T13 | done | COMMIT |
| T20 | [Reject hub invocations once the session ticket has expired](./t20-hub-ticket-expiry.md) | infra | a.udalov | S | T19 | done | COMMIT |
| T21 | [Add the mandatory login and registration-cap edge-case tests](./t21-login-and-cap-edge-tests.md) | tests | a.udalov | S | — | done | COMMIT |
| T22 | [Make the lockout check and failure count atomic under concurrent log-ins](./t22-atomic-lockout.md) | app | a.udalov | M | T21 | done | COMMIT |
| T23 | [Give request-binding failures a ProblemDetails errorCode](./t23-binding-failure-error-code.md) | ports | a.udalov | S | T22 | done | COMMIT |
| T24 | [Apply the client-header check to negotiate with a trailing slash](./t24-negotiate-trailing-slash.md) | ports | a.udalov | XS | — | done | COMMIT |
| T25 | [Make the module-boundary architecture test see method-body references](./t25-architecture-test-il-scan.md) | tests | a.udalov | S | — | done | COMMIT |
| T26 | [Treat any 401 from an authenticated query as logged out, and keep me fresh briefly](./t26-web-401-clears-me.md) | ui | a.udalov | S | T18 | done | COMMIT |
| T27 | [Keep the Player view and show an error when log-out fails](./t27-web-logout-failure.md) | ui | a.udalov | XS | T26 | done | COMMIT |
| T28 | [Offer Register and Log-in links on the landing page](./t28-web-landing-links.md) | ui | a.udalov | XS | T27 | done | COMMIT |
| T29 | [Move focus to the first invalid field after a failed submit](./t29-web-field-error-focus.md) | ui | a.udalov | XS | T28 | todo | — |

**Total:** 29 tasks (T19–T29 are follow-ups from `_review/review-2026-11-14.md`).
