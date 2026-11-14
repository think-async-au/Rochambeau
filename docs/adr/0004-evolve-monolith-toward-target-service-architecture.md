---
status: Accepted
owner: "a.udalov"
reviewers: []
updated_at: "2026-11-14"
feature_size: "n/a (foundation)"
ticket: "docs/target-architecture.md"
---

# 0004 — Evolve the modular monolith toward the target service architecture by measured extraction

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), after comparing the course project «Rochambeau-Online» with the current foundation

## Context

The owner's course project (summarised in [`docs/target-architecture.md`](../target-architecture.md)) designs the portal as microservices: one database per service, a message queue with a global-bus service, caches, sharding, read/write replicas, and load assumptions of 100k users and about 10k writes/s. The repo was founded as a modular monolith (ADR-0002) with one database and a schema per module (ADR-0003), and with a rule of no broker or cache without an ADR. The capacity constraint is unchanged: a solo developer at 1–2 h/day (idea brief §5). The brief's main risk is stalling before anything is showable.

## Decision drivers

- Reach a playable, showable system early (idea brief §6 risk 1).
- Keep the high-load design as a real, demonstrable destination: it is the subject of the owner's course.
- Add scaling mechanisms only when a measurement shows they are needed, so each one can be explained with data.
- Module boundaries must line up with the target services so later extraction is mechanical.

## Considered options

1. **Target vision, evolve.** Keep the modular monolith. Map each module to the target services, keep the seams those services need, and adopt each scaling mechanism (service extraction, queue, cache, replicas, partitions) through its own ADR after a load test justifies it.
2. **Microservices now.** Supersede ADR-0002/0003 and re-scaffold as separate services with a message queue before the first feature.
3. **Feature ideas only.** Take the product ideas from the course project and ignore its architecture.

## Decision outcome

**Chosen:** Option 1. It keeps ADR-0002 and ADR-0003 in force and the delivery risk low, while making the course design an explicit destination with a path to reach it. Option 2 front-loads distributed-systems infrastructure before a single match can be played, which is the stall risk the brief warns about. Option 3 discards the part of the work the owner most wants to demonstrate.

Rules that follow:
- `docs/target-architecture.md` is the destination; `docs/architecture-map.md` stays the description of what exists.
- Each module's `Contracts/` follows the target service boundaries in the mapping table there. For example, Identity keeps authentication separate from roles and permissions, matching Auth service vs User service.
- Load assumptions in the target are **not** NFRs for current features until a load-testing milestone (roadmap) measures the monolith against them.
- Adopting a broker, cache, separate database, replica or service requires its own ADR citing the measurement that motivated it.

## Consequences

**Positive**
- A playable portal arrives early, and each later scaling step has a before/after measurement to show.
- The target design stays visible and traceable instead of living only in a course PDF.

**Negative**
- The high-load parts (queue, sharding, analytics store) arrive late in the roadmap.
- Seams for future extraction are kept by convention; a careless cross-module dependency makes extraction harder.

**Neutral**
- The stored-procedure idea for leaderboards conflicts with the current «no stored procedures» convention and is left to a future ADR.

## Links

- Target: [[../target-architecture.md]]
- Related ADR: [[0002-use-modular-monolith-with-feature-folders]], [[0003-use-ef-core-single-dbcontext-schema-per-module]]
- Roadmap: [[../roadmap.md]] (scale milestone, new steps from the target)
