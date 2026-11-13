---
status: Draft
owner: "a.udalov"
updated_at: "2026-11-13"
depth: "medium"
---

# Idea brief — rochambeau-portal

## 1. Raw idea

> A gaming portal dedicated to a popular game, offering the option to play online against a chosen or random opponent, participate in competitions and tournament leagues, and chat during games or on a dedicated forum.
>
> The portal consists of three components:
> 1. Game portal
> 2. Forum
> 3. Admin section

## 2. Problem

The game is classic rock-paper-scissors (owner-reported). The main goal is a **learning / portfolio project** (owner-reported): build and show a full-stack system that covers real-time head-to-head play, competitions and tournament leagues, in-game and forum communication, and administration. Nobody outside the project has a problem that needs solving. Success means a working, well-engineered system that can be shown to others. It is not measured by audience growth.

## 3. Users

- **Players**: play RPS online against a chosen or random opponent, join competitions and leagues, and chat during games and on the forum (owner-reported, from §1).
- **Administrators**: use the admin section (owner-reported, from §1). What they manage is not yet established.
- **Portfolio reviewers** (e.g. employers): see the project as a showcase. This is an assumption based on the "portfolio" purpose.
- No real player audience is expected yet (owner-reported).

## 4. Why now

No external trigger or deadline (owner-reported: no deadline given). The project is driven by personal learning and building a portfolio.

## 5. Out of scope

Constraints (owner-reported):
- **Solo developer, 1–2 hours a day.** This is the main limit on capacity.
- Preferred stack: **.NET and PostgreSQL** ("part of my stack"). The owner is open to other suggestions. Real-time transport is likely **WebSockets, e.g. SignalR**. This is a preference and is not locked.
- Web, desktop and mobile are all planned eventually. **Start with web.**

Out of scope for the **first slice** (owner-accepted: "core play first"):
- Desktop and mobile clients. They remain part of the long-term vision.
- Competitions, tournaments and leagues. Deferred to later roadmap steps, not dropped.
- The forum. Deferred to a later roadmap step.
- Admin features beyond the minimum needed to run core play.
- Monetisation and growth work. No real audience is targeted (owner-reported purpose).

## 6. Risks

1. **Scope against capacity (weakest assumption).** The full vision (real-time play, tournaments, leagues, in-game chat, forum, admin, then desktop and mobile) is large for one person working 1–2 h/day. The risk is that the project stalls before it can be shown. The owner accepted this as the main driver for starting with core play. This is reversible: the scope can be re-ordered at any step.
2. **Real-time fairness of simultaneous moves.** In RPS both players choose at the same time. If one client can see the other's move before committing, the game is broken. This is a design concern for the core-play spec, not a reason to change direction (assumption, standard property of the game).
3. **Random matchmaking with few concurrent users.** With no real audience, a "random opponent" may rarely find a match. This affects demos more than correctness. It is an assumption, not observed.
4. **Stack lock-in.** The stack is preferred (.NET, PostgreSQL, SignalR), not locked. Choosing it is cheap now and costly to change after core play is built. That decision belongs to design or survey.

Evidence ledger:

| Claim | Class | Origin | Finding / limit |
|---|---|---|---|
| Game is rock-paper-scissors | owner-reported | Interview Q2 | Repo name "Rochambeau" is consistent |
| Purpose is learning / portfolio | owner-reported | Interview Q3 | No audience or adoption target |
| Solo, 1–2 h/day, no deadline | owner-reported | Interview Q4 | — |
| Stack preference .NET / PostgreSQL / SignalR, open to suggestions | owner-reported | Interview Q4 | Preference, not a decision |
| Web first; desktop and mobile later | owner-reported | Interview Q4 | — |
| Full scope is too large for the available capacity to finish quickly | assumption | Interviewer estimate | Not measured; the owner accepted the "core play first" ordering on this basis |
| Repo is empty (greenfield) | source-verified | `git ls-files` at commit 41d1a47, 2026-11-13 | No code, docs or settings existed before this brief |

Critique: done inline by the interviewer, not by an independent reviewer. No consequential access, data or public-contract commitments are made at this stage, so no `devils-advocate` dispatch was needed. No external research: no factual gap would change the direction.

## 7. Recommendation

**Direction (owner-accepted):** Build Rochambeau as a web-first RPS portal for learning and portfolio use, solo. Deliver it in increments, starting with **core play**:
- accounts
- real-time 1v1 RPS against a random or a chosen opponent
- in-game chat
- minimal admin (e.g. banning a user)

Later steps add competitions, tournaments and leagues, then the forum, a fuller admin section, and finally desktop and mobile clients.

**Observable intended result of the first slice:** two players in separate browsers can find each other (randomly or by choice), play an RPS match in real time, chat during it and see the result. An admin can moderate a user.

**Main tradeoff:** a showable result comes early, but the "three components" vision is delivered late. The forum and tournaments show nothing until later steps.

**Alternatives considered:**
- **Thin slice of all three components:** rejected by the owner. It spreads limited hours across three shallow parts.
- **Full scope before showing:** rejected by the owner. It has the highest risk of never reaching a showable state.
- **Reusing an off-the-shelf forum** instead of building one: not decided. It is credible, but building may itself be the portfolio goal. Recorded in §8.

**Outcome: ready-for-roadmap**

Next action: run `roadmap` to break the vision into ordered steps with dependencies. Then `survey` (greenfield foundation, where the stack is confirmed) and `specify` for the core-play step.

## 8. Open questions

| Question | Consequence | Blocking? | Verification action | Owner |
|---|---|---|---|---|
| Final tech stack: confirm .NET, PostgreSQL and SignalR, or choose alternatives | Shapes the skeleton and every later step | Nonblocking for roadmap; blocking for scaffold | Decide in `survey` (greenfield foundation session) | a.udalov |
| Build the forum or integrate an existing one | Large effect on effort and portfolio value of step 3 | Nonblocking (later step) | Decide when the roadmap reaches the forum step | a.udalov |
| What the admin section manages beyond moderation | Defines admin scope in later steps | Nonblocking | Clarify during roadmap or specify | a.udalov |
| Match format (single throw, best-of-N, variants like RPS-Lizard-Spock) | Affects game rules and tournament design | Nonblocking for roadmap | Decide in `specify` for core play | a.udalov |
| How tournaments differ from leagues (bracket vs season ranking) | Defines the scope of the competition step | Nonblocking | Clarify when the roadmap reaches that step | a.udalov |
