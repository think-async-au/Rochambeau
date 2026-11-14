---
status: Draft
owner: "a.udalov"
reviewers: ["a.udalov (implementing engineer)", "Tech Lead"]
updated_at: "2026-11-14"
feature_size: "M"
---

# Test plan — accounts

A Visitor registers (unique case-insensitive Username, email, password) and is logged in at once; a Player logs in and out, sees their own Profile with email, opens another Player's public Profile (never the email) by exact Username; the operator-granted Administrator role is recognised and a role change ends every session; Accounts tells other modules who is acting. Upstream: [spec](spec.md) §5–§6.1 · [SAD](sad.md) §6, §8, §10 · [data model](data-model.md) · [contract](contracts/openapi.yaml).

## Levels

`sad.md` declares `target_surfaces: [backend-service, web-frontend]`, so the frontend tiers apply. No `ux-flows.md` / `screens.md` exist for this feature: e2e-through-UI paths come from spec §4 user stories and SAD §6 flows, and component states come from the §5 ACs.

| Level | Scope | Strategy (generic — no tool names) |
|---|---|---|
| Unit | Identity Domain rules (username, email, password, role, lockout and cap policy values), `RegistrationCap` sliding window, the architecture seam rule — no I/O. | In-memory, controlled clock for time-dependent rules. |
| Integration | The Identity module through its real HTTP entry point (and a test hub for real-time) against the database it owns. | An ephemeral real PostgreSQL container per test collection; a controlled clock swapped into the host where time matters. |
| Contract | The API shapes the web client and later clients rely on: real responses vs `contracts/openapi.yaml`. | For this step the integration tests assert the exact property set of each real response (closed shape, no email on the public Profile) instead of a schema validator — decided in review-2026-11-14 (F6); a schema validator is revisited when a second client consumes the API. |
| E2E | <!-- N/A: API-level flows are covered by integration tests that already drive the real HTTP entry point against a real database; user-story flows run as e2e-through-UI --> | — |
| Load | The three numeric latency NFRs in spec §6. | The load tool already in your repo, or e.g. k6 or Locust — spec §6 fixes the measurement inside the integration suite (see NFR validation). |
| Component | Register, Log-in, own Profile and public Profile screens; the logged-in route guard. | Render the screen in isolation with a stubbed API client; assert rendered output and interactions, no full app boot. |
| Visual-regression | <!-- N/A: no screens.md and no approved baseline images for this feature --> | — |
| E2E-through-UI | Critical user-story flows (register, log in, log out) driven through the real SPA. | Real browser against the running API and an ephemeral database. |

## AC coverage

| AC (spec.md §5) | Test name (intent-based) | Level | Expected outcome |
|---|---|---|---|
| AC-01 register — happy path | valid registration creates a Player and logs them in | integration | Account exists with the Player role, the session is set, and the own Profile answers for that Player |
| AC-01 register — happy path | register screen submits once and opens own Profile | component | one submission leads to the own Profile screen |
| AC-01 register — happy path | new visitor reaches own Profile within a minute | e2e-through-UI | from first visit to logged-in Profile in ≤ 60 s with exactly one form submission (spec §7 KPI, QG-5) |
| AC-02 field rules — error | username outside 3–20 characters or with other characters is rejected | unit | lengths 2 and 21 and any character outside a–z, A–Z, 0–9, `_`, `-` are rejected; 3 and 20 are accepted |
| AC-02 field rules — error | look-alike Cyrillic letter in a username is rejected | unit | a username with a Cyrillic «о» is rejected |
| AC-02 field rules — error | username and email edges are trimmed before checking | unit | leading/trailing spaces do not count toward length or validity |
| AC-02 field rules — error | malformed email is rejected | unit | an email that is not well formed is rejected |
| AC-02 field rules — error | password length is the only password rule | unit | 7 and 129 characters rejected; 8 and 128 accepted with no digit, capital or symbol required |
| AC-02 field rules — error | invalid fields return per-field errors and create no Account | integration | each offending field is named with what it must be; no Account is stored |
| AC-02 field rules — error | register screen shows each field error and keeps input except the password | component | error text next to each offending field; username and email kept, password cleared |
| AC-03 taken — invariant | username taken in any letter case is refused | integration | «rocky» is refused when «Rocky» exists; no second Account; the holder is not revealed; «Rocky» still shows as typed |
| AC-03 taken — invariant | email already used is refused without naming the holder | integration | a used email in any letter case is refused with «cannot be used»; no second Account |
| AC-03 taken — invariant | register screen shows the taken-username and unusable-email messages | component | the message appears next to the matching field |
| AC-03b race — invariant | parallel registrations for one username differing only in letter case create exactly one Account | integration | N parallel requests («Paper», «paper», …) → exactly one Account, N−1 «username taken» answers (N kept within the registration cap, or the cap reset, so the cap never decides the outcome) |
| AC-03b race — invariant | parallel registrations for one email create exactly one Account | integration | exactly one Account with that email; every other request is told the email cannot be used |
| AC-04 log in — happy path | correct email in any letter case with edge spaces and password logs in | integration | the session is set and the own Profile answers for that Player |
| AC-04 log in — happy path | successful log-in opens own Profile | component | the Log-in screen leads to the own Profile screen |
| AC-04 log in — happy path | registered player logs in through the UI and sees own Profile | e2e-through-UI | a real browser session is established and the own Profile is shown |
| AC-05 bad credentials — error | unknown email and wrong password get the identical answer | integration | both responses are identical in body and error identity; no session is set in either case |
| AC-05 bad credentials — error | log-in screen shows «email or password is incorrect» | component | the message is shown and the screen stays on Log-in |
| AC-06 lockout — authorization | fifth failure in a row blocks login even with the correct password | integration | after 5 failures, a correct password is refused with «temporarily blocked» and the retry time |
| AC-06 lockout — authorization | correct login succeeds once 15 minutes have passed | integration | with the clock moved past 15 minutes, the correct password logs in |
| AC-06 lockout — authorization | attempts during the block do not extend it | integration | failed attempts at minute 14 do not move the end of the block |
| AC-06 lockout — authorization | after a block ends, five new failures are needed to block again | integration | 4 failures after the block do not block; the 5th does |
| AC-06 lockout — authorization | a successful login before the fifth failure resets the count | integration | 4 failures, a success, then 4 more failures → still not blocked |
| AC-07 log out — happy path | logging out ends this session only, other devices stay logged in | integration | the logged-out client is treated as a Visitor; a second client on the same Account still gets its Profile |
| AC-07 log out — happy path | player logs out and the Profile page asks to log in | e2e-through-UI | the portal shows the Visitor view and opening the Profile asks to log in |
| AC-08 who is acting — cross-context | current account resolves the logged-in Player on an HTTP request | integration | a test endpoint in the test host gets the Player's account identity, username and role, with no new login |
| AC-08 who is acting — cross-context | current account resolves the logged-in Player on a hub call | integration | a test hub gets the same account identity, username and role on invocation |
| AC-08 who is acting — cross-context | current account is nobody for a Visitor and after logout | integration | «nobody» for an anonymous request and for the same device after logging out |
| AC-08 who is acting — cross-context | session survives 3.5 days minus 1 minute idle and ends at 7 days plus 1 minute | integration | with a controlled clock: still the Player at 3.5 days − 1 minute idle; «nobody» at 7 days + 1 minute idle |
| AC-08 who is acting — cross-context | any request, including opening a real-time connection, counts as activity | integration | a request or a real-time connection before the idle limit keeps the session alive past the original 7-day point |
| AC-08 who is acting — cross-context | no type outside Identity depends on Identity internals | unit | the architecture check finds no reference from outside the Identity module to its Domain, Application, Infrastructure or the identity framework types (QG-4) |
| AC-09 own Profile — happy path | own Profile returns username, email, join date and role | integration | all four are present and belong to the logged-in Player |
| AC-09 own Profile — happy path | own Profile response matches the contract | contract | the real response conforms to the own-profile shape in the contract |
| AC-09 own Profile — happy path | own Profile screen shows username, email, join date and role | component | all four are rendered as plain text |
| AC-10 anonymous Profile — authorization | Visitor asking for the own Profile gets no profile details | integration | refused as not logged in; the response carries no profile fields |
| AC-10 anonymous Profile — authorization | Visitor asking for a public Profile gets no profile details | integration | refused as not logged in, whether or not the username exists; no profile fields |
| AC-10 anonymous Profile — authorization | route guard sends a Visitor to log in or register | component | a not-logged-in answer renders no profile data and offers log in / register |
| AC-11 public Profile — happy path | public Profile by exact username in any letter case never contains the email | integration | «scissors» opens «Scissors»; username, join date and role present; the response has no email property at all |
| AC-11 public Profile — happy path | public Profile response matches the contract and has no email field | contract | the real response conforms to the public-profile shape, which has no email |
| AC-11 public Profile — happy path | public Profile screen shows username, join date and role only | component | the three fields are rendered; nothing email-like appears |
| AC-12 unknown username — error | unknown username says no such player | integration | told that no player with that username exists |
| AC-12 unknown username — error | partial username finds nothing | integration | «Scis» does not open «Scissors»; same «no such player» answer |
| AC-12 unknown username — error | public Profile screen shows «no player with that username» | component | the not-found state is rendered |
| AC-13 administrator — happy path | operator-designated administrator is recognised at login | integration | after the role is set through the same role-change path the operator command uses, login yields the Administrator role in the Profile and in the acting identity |
| AC-13 administrator — happy path | own Profile screen shows the Administrator role | component | the role is rendered as Administrator |
| AC-13b role change — authorization | granting the role ends sessions on every device | integration | two logged-in clients are both treated as not logged in on their next request; after a new login the role is Administrator |
| AC-13b role change — authorization | removing the role ends sessions on every device | integration | both clients are logged out; after a new login the role is Player |
| AC-13b role change — authorization | role change closes an open real-time connection on its next call | integration | the next hub invocation on a connection opened before the change is refused and the connection is closed |
| AC-14 no self-escalation — authorization | registration ignores a supplied role | integration | a registration carrying an administrator role creates a Player; no role changes anywhere |
| AC-14 no self-escalation — authorization | no portal endpoint can change a role | integration | the list of mapped endpoints offers no role-changing route; a logged-in Player's attempts leave every Account's role unchanged |
| AC-14 no self-escalation — authorization | a new Account is always a Player | integration | the stored Account's role is Player whatever the request body carries; the Player role is set by the Register handler, not by a Domain rule (covered by `RegisterTests.AC14_a_role_in_the_body_is_ignored_and_the_account_is_a_player`) |
| AC-15 registration cap — authorization | registration cap allows 5 per address per rolling 60 minutes | unit | the 6th reservation within 60 minutes is refused; it is allowed once the oldest is older than 60 minutes (controlled clock) |
| AC-15 registration cap — authorization | a released slot does not count | unit | a reservation released after a failure frees the slot |
| AC-15 registration cap — authorization | sixth registration from one address within 60 minutes is refused | integration | no Account is created; told too many accounts came from this connection and to try later |
| AC-15 registration cap — authorization | failed registrations do not count toward the cap | integration | after failed attempts (invalid fields, taken username) 5 successes are still possible |
| AC-15 registration cap — authorization | ten parallel registrations with four already counted create exactly one Account | integration | exactly one new Account; the other nine are refused with the cap message |
| AC-15 registration cap — authorization | register screen shows the too-many-accounts message | component | the cap message is rendered and no navigation happens |

## Edge cases / error paths

Every error and authorization AC (AC-02, AC-03, AC-03b, AC-05, AC-06, AC-10, AC-12, AC-13b, AC-14, AC-15) has its own rows above. These cases come from spec §6.1 and SAD §8 rather than from a single AC; each is a mandatory test for `implement`.

| Case | Test name (intent-based) | Level | Expected outcome |
|---|---|---|---|
| CSRF guard (accounts ADR-0005) | unsafe request with a valid session but no client header is rejected | integration | rejected before any handler runs; nothing changes |
| CSRF guard (accounts ADR-0005) | real-time connection handshake without the client header is rejected | integration | the connection is not established |
| Blank input (AC-02) | username of spaces only is rejected as too short | unit | after trimming it fails the length rule |
| Missing field (AC-02) | registration with a missing field reports that field | integration | a field error for the missing field; no Account is stored |
| Enumeration (AC-05, SAD §8) | login with an unknown email creates no lockout state | integration | no Account or lockout record appears for that email |
| Lockout isolation (AC-06) | lockout of one Account does not affect another | integration | Account B logs in normally while Account A is blocked |
| Cap isolation (AC-15) | the registration cap is counted per network address | integration | a second address can still register while the first is capped |
| Log out without a session (AC-07) | logging out with no session changes nothing | integration | treated as not logged in; no other session is affected |
| Operator command (AC-13) | role change for an unknown username fails without side effects | integration | reports that no such account exists, exits with failure, and no role changes |
| Database unavailable | registration fails closed when the database is unreachable | integration | an unexpected-error answer; no Account and no consumed cap slot |

Deliberately not asserted: equal response **time** for an unknown email vs a wrong password (SAD §8 dummy-hash check). A timing assertion would be flaky; only identical response bodies are asserted (AC-05 rows).

## Test data

- **Seed strategy:** the data-model fixtures. `RegisterAsync` goes through the real register endpoint, so fixtures pass the same normalisation and constraints as AC-01 to AC-03b. `PromoteToAdministratorAsync` goes through the same role-change handler as the operator command and never writes the role directly. Every test uses its own unique synthetic usernames (`player_<n>`, plus the spec's `Rocky` / `Paper` / `Scissors` made unique per test where they must not collide) and `@example.test` emails. No real personal data.
- **Integration dependency:** an ephemeral real PostgreSQL container per test collection (the repo's existing per-collection fixture), with migrations applied. No mocked store.
- **Isolation of in-memory state:** the registration cap and the controlled clock live in the test host's process. Tests that depend on them (AC-06, AC-08 idle window, AC-15, the register latency run) get a **fresh test host** with its own controlled clock. Every other test registers from a **unique network address**, so the cap never decides its outcome.
- **Cleanup boundary:** per-suite. The container is destroyed when the collection ends; tables are not truncated between tests. Each test asserts only on rows it created (counts filtered by its own usernames/emails). No test asserts the total number of Accounts in the table.
- **Unit and component:** no persisted data. Domain rules and the cap run in memory with a controlled clock; screens render against a stubbed API client.
- **E2E-through-UI:** a fresh ephemeral database per run; each scenario registers its own unique Player.

## NFR validation (load)

Spec §6 fixes the method: p95 of 20 timed requests after 3 warm-up requests, in the integration test suite, local single instance. The tool is the load tool named in Levels. Spec §6 asks for a measurement inside the integration suite, so these run there as a separate category of tests.

- **Latency p95 — register ≤ 500 ms:** 3 warm-up + 20 timed sequential registrations (1 concurrent client) of distinct valid Accounts on a fresh test host with the cap out of the way (fresh host and a distinct network address per request), so the 6th request is not refused. Assert p95 ≤ 500 ms and all 20 succeed.
- **Latency p95 — log in ≤ 400 ms:** 3 warm-up + 20 timed sequential correct logins (1 concurrent client) for one pre-registered Account. Assert p95 ≤ 400 ms and all 20 succeed.
- **Latency p95 — view a profile ≤ 150 ms:** 3 warm-up + 20 timed sequential public-Profile reads (1 concurrent client) by a logged-in Player. Assert p95 ≤ 150 ms and all 20 succeed.
- **Throughput / availability:** <!-- N/A: no numeric NFR — spec §6 marks both TBD, see spec §8 -->

The concurrency NFR (AC-03b) and the session-lifetime NFR (AC-08) are verified by the integration rows above, not by load tests.

## CI placement

Advice only — `implement` and the repo's CI own the real wiring.

- **On every PR:** unit, component, contract and integration (needs a container runtime on the CI machine). These carry the spec §7 KPIs that must hold on every CI run: «Duplicate usernames» (AC-03b) and «Email exposure» (AC-10, AC-11).
- **On schedule / pre-release (and in `ship`):** the three latency checks (tagged as their own category, because timings on shared CI machines are noisy) and the e2e-through-UI scenarios (AC-01 KPI demo, AC-04, AC-07).
