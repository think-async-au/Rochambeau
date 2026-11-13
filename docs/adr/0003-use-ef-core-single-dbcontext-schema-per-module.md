---
status: Accepted
owner: "a.udalov"
reviewers: []
updated_at: "2026-11-13"
feature_size: "n/a (foundation)"
ticket: "docs/idea-brief.md"
---

# 0003 — Persist with EF Core in a single DbContext, one PostgreSQL schema per module, UUIDv7 keys

- **Status:** Accepted
- **Date:** 2026-11-13
- **Deciders:** a.udalov (owner), during the `survey` greenfield foundation session

## Context

PostgreSQL is the datastore (ADR 0001), and the backend is a modular monolith (ADR 0002). We need to decide how code reaches the database, how the schema evolves, how module data is kept apart, and how IDs are generated. Every feature depends on this, and changing it later means migrating all data-access code.

## Decision drivers

- Owner's stack is .NET + PostgreSQL (idea brief §5).
- Module isolation (ADR 0002): one module must not reach into another module's tables.
- Solo capacity (idea brief §5): one migration history is easier to manage than several.
- IDs must be generatable in application code (for testability and for events created before saving) and should sort by creation time for good index locality.

## Considered options

1. **EF Core with one `AppDbContext`.** Each module contributes its own entity configurations mapped to its own PostgreSQL schema. One migration history. `Guid.CreateVersion7()` keys.
2. **EF Core with one DbContext per module.** Stronger isolation, but N migration histories, and transactions across contexts become awkward.
3. **Dapper with hand-written SQL migrations** (e.g. DbUp). Full SQL control, but more code to write by hand and no change tracking.

## Decision outcome

**Chosen:** Option 1. A single context and migration history keep the workflow simple for one developer. Schema-per-module plus the `Contracts/` rule from ADR 0002 keep module data separate at the database level. UUIDv7 keys are time-sortable and generated in code. Option 2 stays possible later: a module's configurations can move into its own context without renaming tables.

## Consequences

**Positive**
- `dotnet ef migrations add` / `database update` is the whole schema workflow. Every migration ships with a `Down`.
- Schemas (`identity`, `play`, `chat`, `admin`, …) make module ownership visible in the database.
- Keys are known before saving, which simplifies tests and SignalR broadcasts.

**Negative**
- A single context means nothing technically stops a query that joins across modules. Only the convention prevents it.
- EF Core abstractions can hide inefficient SQL. Hot paths should be checked with logging or `ToQueryString()`.

**Neutral**
- snake_case naming via `EFCore.NamingConventions` keeps SQL idiomatic for PostgreSQL. Changing the naming later would need a migration that renames everything.

## Links

- Idea brief: [[../idea-brief.md]] §5
- Architecture map: [[../architecture-map.md]] §Conventions, §Datastores
- Related ADR: [[0001-use-dotnet-react-postgres-signalr-stack]], [[0002-use-modular-monolith-with-feature-folders]]
