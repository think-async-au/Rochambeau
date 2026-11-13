---
status: Accepted
owner: "a.udalov"
reviewers: []
updated_at: "2026-11-13"
feature_size: "n/a (foundation)"
ticket: "docs/idea-brief.md"
---

# 0002 — Organize the backend as a modular monolith with per-module feature folders

- **Status:** Accepted
- **Date:** 2026-11-13
- **Deciders:** a.udalov (owner), during the `survey` greenfield foundation session

## Context

The product has several distinct areas: identity, live play, chat and admin first, then competitions and a forum (idea brief §1, §7). They need clear boundaries so later steps can be added without tangling the code. At the same time, one developer with 1–2 h/day cannot afford to operate several deployables.

## Decision drivers

- Solo developer with limited time (idea brief §5): one thing to build, run and deploy.
- Incremental roadmap (idea brief §7): new areas (Competitions, Forum) must slot in as new modules.
- Portfolio value: visible, enforced boundaries show architectural discipline.
- Real-time play needs low-latency in-process access to match state.

## Considered options

1. **Modular monolith.** One ASP.NET Core app. Each module is a folder (`Modules/<Name>/`) with Domain, Application, Infrastructure and Endpoints/Hubs layers, a static registration class, and a public `Contracts/` namespace for anything other modules may call.
2. **Modular monolith with one class-library project per module.** Boundaries are enforced by the compiler, at the cost of more projects and solution overhead.
3. **Plain layered app** (`Controllers/`, `Services/`, `Repositories/` across the whole app). Simple, but features get spread across layers and there are no module boundaries.

## Decision outcome

**Chosen:** Option 1. It keeps a single project and deployable, which fits the capacity limit. It still gives each area its own folder, schema and public contract, so new modules from the roadmap slot in cleanly. Option 2 can be adopted later by moving a folder into a project, because the dependency rules are the same.

## Consequences

**Positive**
- There is one build and one deploy. Calls between modules are plain in-process calls.
- Each feature is a vertical slice inside its module, so a change touches a single place.

**Negative**
- Module boundaries are enforced by convention, not by the compiler. A careless `using` can cross them. An architecture test (e.g. NetArchTest) can be added if this becomes a problem.

**Neutral**
- Extracting a module into its own project (Option 2) or its own service later is possible. The `Contracts/` rule keeps that move mechanical.

## Links

- Idea brief: [[../idea-brief.md]] §7
- Architecture map: [[../architecture-map.md]] §Module inventory, §Conventions
- Related ADR: [[0001-use-dotnet-react-postgres-signalr-stack]], [[0003-use-ef-core-single-dbcontext-schema-per-module]]
