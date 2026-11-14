---
status: living
updated_at: "2026-11-14"
source: "Owner's course project «Rochambeau-Online» (Highload architecture, PDF, 8 pages, kept outside the repo)"
---

# Target architecture — Rochambeau-Online

> **A destination, not the current build.** This is the owner's high-load design for the
> portal, summarised from their course project. The repo builds toward it **incrementally**:
> today it is a modular monolith ([ADR-0002](adr/0002-use-modular-monolith-with-feature-folders.md),
> [ADR-0003](adr/0003-use-ef-core-single-dbcontext-schema-per-module.md)). Parts of this target
> are adopted one at a time, each by its own ADR, when a measurement justifies it
> ([ADR-0004](adr/0004-evolve-monolith-toward-target-service-architecture.md)).
> `docs/architecture-map.md` describes what exists; this file describes where it is heading.

## Product scope in the target

- **Game portal.** The lobby shows players online with their rating, a Top-100, upcoming competitions the player qualifies for by rating, and a global chat. The player picks an opponent, or a random one, and the opponent confirms. The two players then **negotiate a game style**: one proposes, the other accepts or counter-proposes until they agree. In the match each player moves within a **timer**, then the winner is announced. Styles include variants beyond the classic three, e.g. a 7-gesture set: Rock, Fire, Scissors, Sponge, Paper, Air, Water.
- **Forum.** News about upcoming competitions, posts and comments by role and permission. A **Moderator** publishes news and posts, deletes posts and comments, and bans offenders. An anti-spam component (a text classifier) flags spam and triggers warnings or bans.
- **Admin panel.** Administrators add and edit game styles (scenarios), competitions and leagues, and monitor game statistics through analytics.
- **Analytics.** Tracking agents collect player and forum actions: games per period, average game duration, time between moves (average and peak), and move sequences for detecting patterns and strategies. The data is aggregated into a separate analytics store.
- **Clients.** Browser and mobile app. Login goes through an authentication service with sessions and 2FA. Authorization (roles, permissions) is a separate user service.

## Target services → current modules

| Target service (PDF) | Responsibility | Current / planned module | Seam to keep now |
|---|---|---|---|
| Auth service | login, logout, authentication, sessions, 2FA | Identity | keep authentication apart from roles/permissions inside Identity's contracts |
| User service | authorization, roles, permissions, user profile | Identity | role/permission checks go through Identity `Contracts/`, never through its tables |
| Player service | player profile, game history, competition participation, rating | Play (player side) | Player data is keyed by account identity from Identity, never by copying user data |
| Game service | create game, make move, decide the result | Play (match engine) | match results written in one transaction (win + loss together) |
| Games configuration service | game styles, game settings, leagues, tournaments | Competitions + Admin (later) | game style is a value Play reads through a contract |
| In-game chat service | lobby chat (global) + match chat (private) | Chat | — |
| Forum service + anti-spam | news, posts, comments, moderation | Forum (later) | — |
| Analytics service + tracking agents | collect and query user and game events | none yet (fog) | emit domain events in-process, so a tracker can subscribe later |
| Global bus / message queue | async integration between services | none — in-process calls (ADR-0002) | `Contracts/` interfaces are the future message boundaries |

## Load assumptions (owner estimates, not measured)

- 100k users; requests mostly writes, about 100:1 writes to reads; payloads under about 50 bytes.
- A game lasts about 10 s → about 10k writes/s and about 100 reads/s; about 1 TB of new records a month.
- Quality goals: reliability, scalability, maintainability; fast UI update for every player after each move.

These are **assumptions**. The project has no real audience (idea brief §5). They become targets for a later load-testing milestone (roadmap), measured against the monolith before any scaling mechanism is added.

## Scaling mechanisms in the target (each needs its own ADR before adoption)

- Microservices with one database per service; synchronous client calls plus a message queue owned by a global-bus service; a saga for style negotiation.
- Caches on read-heavy entities, with a per-entity lifetime, and in-memory caches for very hot data.
- Game service: separate write and read nodes, replication, partitioning (e.g. by date or ID), batch synchronisation to read replicas.
- Stored procedures for leaderboard queries. This currently conflicts with the architecture-map rule «no stored procedures or triggers» and would need an ADR.
- Logging, metrics, monitoring and automated tests from the start. Named metrics: response time, error rate, games per period by time of day, game duration, time between moves, database read/write latency, replica sync time.

## Tension to resolve when the match engine is designed

The PDF asks for **ACID** on game results (a win for one player and a loss for the other must be written together, read committed or repeatable read). It also suggests **eventual consistency** with batched replica sync and «AP / PA-EL» availability. Both can hold, with ACID on the write side and eventually consistent read models, but the split must be decided explicitly in `/sdd:design` for the match step.
