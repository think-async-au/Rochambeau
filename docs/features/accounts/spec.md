---
status: Draft
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
---

# Spec — accounts

> **Glossary:** [CONTEXT](../../../CONTEXT.md) (repo-root; no feature-scoped CONTEXT)
> **Reference module / docs / channels used:** `docs/idea-brief.md` §3, §5, §7 · `docs/roadmap.md` step 2 · `docs/architecture-map.md` (module inventory, constraints). No reference module exists yet.

## 1. Context

Rochambeau is a web portal where people play real-time rock-paper-scissors against each other. Today nobody can be identified: every later step depends on knowing *who* is acting. A match is played between two named players, chat messages have authors, and an Administrator moderates a specific person. This feature gives a Visitor a way to become a Player, with an Account, a public Username and a Profile (`idea-brief.md` §3, §7; `roadmap.md` step 2).

Why now: Accounts is the first feature step after the scaffold and blocks both the match against a chosen opponent (step 3) and minimal moderation (step 6). Nothing showable can be built on top of anonymous users. The project is solo, at 1–2 hours a day, with no deadline (`idea-brief.md` §5). The slice is kept to what those two steps need.

Committed approach: a Visitor registers with a unique Username, an email address and a password, and is logged in right away. A Player logs in with email and password and stays logged in across the portal until logging out or going idle. Every Player has a Profile; other logged-in Players can see its public part, so a future opponent can be looked up by Username. The administrator role exists from day one and is granted only by whoever operates the portal, not through the portal itself. Moderation tools themselves are step 6.

Traceability: the scope is the "accounts" bullet of the owner-accepted core-play direction (`idea-brief.md` §7). The constraint to keep the server API client-agnostic for future desktop/mobile clients comes from `architecture-map.md` · Constraints and `idea-brief.md` §5. The choices recorded in this spec were made at `easy` interview depth: the owner accepted all ten defaults in the assumptions ledger as a batch, then accepted all six critic amendments. These were: throughput moved to an open question, the email-confirmation question dropped, the enumeration risk made explicit, AC-08 tied to the identity other areas consume, AC-03b extended to email, and AC-15 added for the registration cap.

## 2. Goals

- A Visitor can go from first visit to a logged-in Player in one short, self-explanatory flow, with no help from an Administrator.
- Every action in later steps (playing, chatting, moderating) can be attributed to exactly one Account, identified publicly by a Username that never clashes with another.
- Account credentials are protected well enough that the portal can be shown publicly as a portfolio piece without embarrassing weaknesses, such as guessable logins or leaked emails.

## 3. Non-goals

- **Email confirmation and password reset.** The portal sends no email in this step: the project has no real audience, and sending mail adds an external dependency. The email is stored so both can be added later without migrating accounts.
- **Editing the profile or changing the username.** The username is the identity other players know and challenge. Keeping it fixed avoids renaming problems until there is a real need.
- **Deleting or deactivating an account, and banning.** Banning belongs to minimal moderation (roadmap step 6). Self-deletion has no audience need yet.
- **Social or third-party login** (signing in with an outside account provider). It is extra integration work with no portfolio value at this stage.
- **Granting the administrator role through the portal.** There is no admin section yet (roadmap step 9). The role is granted by whoever operates the portal.

## 4. User stories

### US-01: Register a new account

**As a** Visitor
**I want** to register with a username, an email address and a password
**So that** I become a Player and can use the portal under my own name

### US-02: Log in

**As a** Visitor who already has an Account
**I want** to log in with my email address and password
**So that** the portal recognises me as my Player again

### US-03: Log out

**As a** Player
**I want** to log out
**So that** nobody else using this browser can act as me

### US-04: See my own profile

**As a** Player
**I want** to see my Profile, including my private email address
**So that** I can check how my account looks and which details the portal holds about me

### US-05: Look up another player's profile

**As a** Player
**I want** to open another Player's public Profile by their Username
**So that** I know who they are before I challenge them to a match

### US-06: Be recognised as an administrator

**As an** Administrator
**I want** the portal to recognise my administrator role when I log in
**So that** moderation tools can be offered to me, and only to me, once they exist

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** a Visitor who is not logged in
**When** the Visitor registers with a username, an email address and a password that are all valid and not yet taken
**Then** the system creates the Account with the Player role, logs the Visitor in as that Player straight away, and shows their Profile

### AC-02 (US-01) — error

**Given** a Visitor on the registration form
**When** the Visitor submits a username shorter than 3 or longer than 20 characters, or with characters other than letters, digits, underscore or hyphen, or an email address that is not well formed, or a password shorter than 8 characters
**Then** the system creates no Account, and shows next to each offending field what it must be (for example "the password must be at least 8 characters"), keeping what the Visitor typed except the password

### AC-03 (US-01) — domain invariant

**Given** an Account with the username "Rocky" already exists
**When** a Visitor tries to register with the username "rocky" (any letter case), or with an email address that an existing Account already uses
**Then** the system creates no Account and tells the Visitor that this username is already taken, or that this email address cannot be used, without revealing whose Account holds it

### AC-03b (US-01) — domain invariant, concurrent

**Given** two Visitors who both choose the username "Paper" (or the same email address) at the same moment, and no Account holds it yet
**When** both submit their registration
**Then** exactly one Account is created with that username (or email address), and the other Visitor is told the username is already taken (or the email address cannot be used)

### AC-04 (US-02) — happy path

**Given** a Visitor whose Account exists
**When** the Visitor logs in with the correct email address and password
**Then** the system logs them in as their Player and shows their Profile

### AC-05 (US-02) — error

**Given** a Visitor on the login form
**When** the Visitor enters an email address that has no Account, or a wrong password for an existing one
**Then** the system does not log them in, and shows the same message in both cases ("email or password is incorrect"), so the login form does not reveal which email addresses are registered

### AC-06 (US-02) — authorization

**Given** an Account that has had 5 failed login attempts in a row
**When** anyone tries to log in to that Account again within the next 15 minutes, even with the correct password
**Then** the system refuses the login and says that login is temporarily blocked and when it can be tried again. After the 15 minutes, a correct login succeeds and resets the count

### AC-07 (US-03) — happy path

**Given** a logged-in Player
**When** the Player logs out
**Then** the system ends their session, shows the portal as a Visitor sees it, and going back to the Profile page asks them to log in

### AC-08 (US-03, US-02) — cross-context

**Given** a Player who is logged in, and another area of the portal (such as play, chat or moderation) that needs to know who is acting
**When** that area asks Accounts who is behind the current request
**Then** Accounts answers with that Player's account identity, username and role, without the Player logging in again. It answers "nobody" for a Visitor, after the Player logs out, or after 7 days without activity

### AC-09 (US-04) — happy path

**Given** a logged-in Player
**When** the Player opens their own Profile
**Then** the system shows their username, email address, join date and role

### AC-10 (US-04, US-05) — authorization

**Given** a Visitor who is not logged in
**When** the Visitor tries to open any Profile, their own or someone else's
**Then** the system shows no profile details and asks them to log in or register first

### AC-11 (US-05) — happy path

**Given** a logged-in Player and another Player with the username "Scissors"
**When** the first Player opens the Profile of "Scissors"
**Then** the system shows that Player's username, join date and role, but never their email address

### AC-12 (US-05) — error

**Given** a logged-in Player
**When** the Player looks up a username that no Account holds
**Then** the system tells them that no player with that username exists

### AC-13 (US-06) — happy path

**Given** an Account that the portal's operator has designated as an administrator
**When** its owner logs in
**Then** the system recognises them as an Administrator and their Profile shows the Administrator role

### AC-14 (US-06) — authorization

**Given** a Visitor registering, or a logged-in Player
**When** they try to give themselves, or anyone else, the administrator role through the portal
**Then** the system offers no way to do so, and any attempt leaves every Account's role unchanged

### AC-15 (US-01) — authorization

**Given** 5 Accounts have already been registered from the same network address within the last hour
**When** a Visitor from that address tries to register another Account
**Then** the system creates no Account and tells the Visitor that too many accounts were created from their connection recently and to try again later

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Latency p95 — register | ≤ 500 ms (includes deliberately slow password protection) | timing assertion in the integration test suite, local single instance |
| Latency p95 — log in | ≤ 400 ms | timing assertion in the integration test suite, local single instance |
| Latency p95 — view a profile | ≤ 150 ms | timing assertion in the integration test suite, local single instance |
| Throughput | TBD — no hosting or instance size chosen yet | see §8 |
| Availability | TBD — no hosting chosen yet | see §8 |
| Concurrency | two registrations racing for the same username or email → exactly one succeeds (AC-03b) | integration test firing parallel registrations |
| Session lifetime | logged in until logout or 7 days without activity | integration test with a controlled clock |

## 6.1 Security / privacy

- **Data classification:** confidential. Accounts hold credentials and an email address; Usernames and join dates are public to logged-in Players.
- **Personal data touched:** email address (string, personal data, visible only to its owner); password (never stored in readable form; stored only as a one-way protected value); username (string, public); join date (timestamp, public); last-activity time (timestamp, internal).
- **AuthZ/AuthN impact:** this feature introduces authentication for the whole portal and two roles, Player and Administrator. Checks: profile details are available only to logged-in Players; the email address is returned only to its owner; the administrator role can only be set by the portal's operator, outside the portal; repeated failed logins block that account temporarily.
- **Abuse cases:**
  - Account enumeration (probing which emails or usernames exist): login gives the same message for unknown email and wrong password (AC-05). **Accepted residual risk:** registration reveals that an email is already in use (AC-03), and the lockout message appears only for existing accounts (AC-06). Both are limited by the registration cap (AC-15) and the lockout itself; closing them fully would need email confirmation, a §3 non-goal. Registration says a username is taken, which is unavoidable for a public name.
  - Password guessing on one account: 5 failures in a row block the account for 15 minutes (AC-06).
  - Mass account creation (spam): at most 5 registrations per hour from the same network address; beyond that the Visitor is told to try again later (AC-15).
  - Email leak through profiles: other Players' profiles never include the email (AC-11).
  - Privilege escalation: no portal action can grant the administrator role (AC-14).
  - Injection through text fields: the username is restricted to letters, digits, underscore and hyphen (AC-02), and all text is shown as plain text, never interpreted.
- **Security review:** Required — new authentication boundary and new personal data (email).

## 7. Metrics / KPIs

- **Registration-to-play readiness** — baseline: 0 (no accounts possible), target: in a scripted end-to-end demo, a new Visitor reaches their logged-in Profile in ≤ 60 s and ≤ 1 form submission, by the end of this step.
- **Duplicate usernames** — baseline: 0, target: 0 Accounts whose usernames differ only by letter case, checked by the concurrency test (AC-03b) on every CI run.
- **Email exposure** — baseline: 0, target: 0 places where a Player can see another Player's email, checked by authorization tests (AC-10, AC-11) on every CI run.
- **Downstream readiness** — baseline: steps 3 and 6 blocked, target: both can start their spec using only this feature's public identity (account identity + username + role), with no new Accounts work, at the time step 3 is specified.

## 8. Open questions

- [ ] What availability and throughput targets apply once the portal is hosted for demos? Default now: none (local single instance only). — owner: a.udalov, due: when a hosting choice is made (before the first public demo)
