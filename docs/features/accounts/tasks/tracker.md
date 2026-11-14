# Tracker — accounts

> Status of every task in the epic. `implement` updates **Status** and **Step** after every
> subagent it runs (and commits them with the task), so this table shows where the run is right now.
> Status: `todo` · `in_progress` · `blocked` · `review` · `done`.
> Step: `SELECT` · `RED` · `GREEN` · `REFACTOR` · `GATE` · `REVIEW` · `COMMIT` (`—` before the task starts).
> A `blocked` row carries its reason in Step, e.g. `GATE — lint red after 3 retries`.

| # | Task | Layer | Owner | Estimate | Blocked by | Status | Step |
|---|---|---|---|---|---|---|---|
| T1 | [Add Result/Error, the ProblemDetails mapping and the client-header filter to Common](./t01-common-results-problemdetails-client-header.md) | ports | a.udalov | M | — | todo | — |
| T2 | [Encode the Identity domain rules: username, email, password, role, lockout and cap policy](./t02-identity-domain-rules.md) | domain | a.udalov | S | — | todo | — |
| T3 | [Map the Account entity to identity.accounts and promote the staged migration](./t03-account-entity-and-migration.md) | migration | a.udalov | M | T2 | todo | — |
| T4 | [Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule](./t04-identity-core-cookie-session-wiring.md) | wiring | a.udalov | M | T1, T3 | todo | — |
| T5 | [Expose ICurrentAccount and IAccountDirectory through Identity Contracts](./t05-identity-contracts-current-account.md) | infra | a.udalov | M | T4 | todo | — |
| T6 | [Implement the in-process RegistrationCap sliding window](./t06-registration-cap.md) | app | a.udalov | S | T2 | todo | — |
| T7 | [Register an Account through POST /api/v1/accounts](./t07-register-endpoint.md) | ports | a.udalov | M | T5 | todo | — |
| T8 | [Enforce the registration cap and prove the race guard on registration](./t08-registration-cap-and-race-guard.md) | app | a.udalov | M | T6, T7 | todo | — |
| T9 | [Log in through POST /api/v1/session with lockout and enumeration-safe errors](./t09-login-endpoint-lockout.md) | ports | a.udalov | M | T7 | todo | — |
| T10 | [Serve GET /api/v1/me and log out with DELETE /api/v1/session](./t10-own-profile-and-logout.md) | ports | a.udalov | M | T9 | todo | — |
| T11 | [Serve the public Profile through GET /api/v1/profiles/{username}](./t11-public-profile-endpoint.md) | ports | a.udalov | S | T7 | todo | — |
| T12 | [Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow](./t12-operator-command-change-role.md) | app | a.udalov | M | T10 | todo | — |
| T13 | [Revalidate the session on every hub invocation with SessionRevalidationHubFilter](./t13-session-revalidation-hub-filter.md) | infra | a.udalov | M | T12 | todo | — |
| T14 | [Measure the spec §6 latency targets for register, log in and view a profile](./t14-latency-nfr-tests.md) | tests | a.udalov | S | T8, T9, T11 | todo | — |
| T15 | [Add the shared API client and the «me» query to the SPA](./t15-web-api-client-and-me-query.md) | ui | a.udalov | S | — | todo | — |
| T16 | [Build the Register screen with field-level errors](./t16-web-register-screen.md) | ui | a.udalov | M | T15 | todo | — |
| T17 | [Build the Log-in screen, the log-out action and the logged-in route guard](./t17-web-login-logout-route-guard.md) | ui | a.udalov | M | T16 | todo | — |
| T18 | [Build the own Profile and public Profile screens](./t18-web-profile-screens.md) | ui | a.udalov | M | T17 | todo | — |

**Total:** 18 tasks, ~15.5 person-days (S ≈ ½ day, M ≈ 1 day).
