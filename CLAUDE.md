# Rochambeau

Web-first rock-paper-scissors portal (real-time 1v1 play, chat, competitions, forum, admin) — a
solo learning / portfolio project. Product intent: `docs/idea-brief.md`. Plan: `docs/roadmap.md`.
Architecture: `docs/architecture-map.md` (what exists) + `docs/target-architecture.md` (high-load destination, ADR-0004) + ADRs in `docs/adr/` (0001 stack, 0002 modular monolith,
0003 single DbContext / schema per module). Those documents win over this file if they disagree.

## Prerequisites

.NET 10 SDK (pinned by `global.json`), Node.js LTS + npm, Docker Desktop (local PostgreSQL and
Testcontainers integration tests).

## Commands

| Task | Command |
|---|---|
| Build | `dotnet build Rochambeau.slnx && npm --prefix web run build` |
| Test | `dotnet test Rochambeau.slnx && npm --prefix web test` (integration tests need Docker running) |
| Lint | `dotnet format Rochambeau.slnx --verify-no-changes && npm --prefix web run lint` |
| Fix formatting | `dotnet format Rochambeau.slnx` · `npm --prefix web run format` |
| Local DB | `docker compose up -d` (PostgreSQL 17 on :5432, db/user/password `rochambeau`) |
| Run API | `dotnet run --project src/Rochambeau.Api` (http://localhost:5168; applies migrations in Development) |
| Run web | `npm --prefix web run dev` (Vite proxies `/api` and `/hubs` to the API) |
| Operator: grant / revoke admin | `dotnet run --project src/Rochambeau.Api -- grant-admin <username>` · `... -- revoke-admin <username>` (runs and exits; ends the Account's sessions) |
| Restore tools | `dotnet tool restore` (dotnet-ef) |
| Add migration | `dotnet ef migrations add <PascalName> --project src/Rochambeau.Api --output-dir Common/Persistence/Migrations` |
| Apply / revert | `dotnet ef database update --project src/Rochambeau.Api` · `... database update 0` |

Warnings are errors (`Directory.Build.props`: nullable, analyzers `Recommended`, code style in build).

## Layout

- `src/Rochambeau.Api/` — the single ASP.NET Core host (modular monolith). `Program.cs` only wires
  cross-cutting concerns and calls each module's `Add<Name>Module` / `Map<Name>Module`.
- `src/Rochambeau.Api/Common/` — shared kernel: `Results/` (`Result<T>` / `Error`), `Http/` (Error →
  ProblemDetails, client-header filter), `Commands/` (operator-command registry + the single
  `Program.cs` dispatch hook), `Time/`, `Persistence/` (`AppDbContext`, migrations).
- `src/Rochambeau.Api/Modules/<Name>/` — one module per bounded area (Identity, Play, Chat, Admin;
  later Competitions, Forum). Layers `Domain → Application → Infrastructure / Endpoints / Hubs / Commands`
  (`Commands/` = operator commands run on the host, e.g. `grant-admin <username>`), plus
  `Contracts/` (public interfaces + DTOs) and `<Name>Module.cs`.
- `tests/Rochambeau.UnitTests/<Module>/` — xUnit, no I/O.
- `tests/Rochambeau.IntegrationTests/<Module>/` — xUnit + `WebApplicationFactory` + Testcontainers
  PostgreSQL (`Infrastructure/PostgresFixture`, one container per collection; `ApiFactory`).
- `web/` — React + TypeScript + Vite SPA. Screens in `web/src/features/<feature>/`, shared
  primitives in `web/src/shared/ui/`, one SignalR connection hook per hub in
  `web/src/shared/realtime/`. Tests colocated as `*.test.tsx` (Vitest + React Testing Library).

## Conventions

- **Module boundaries:** a module calls another only through that module's `Contracts/` namespace —
  never its entities or tables. Domain has no framework dependencies. No message broker; in-process calls.
- **Errors:** expected failures return `Result<T>` / `Error` (code, message, type), not exceptions.
  HTTP maps them to ProblemDetails (RFC 9457) with an `errorCode` extension; hub methods return
  `HubResult<T>`. Unexpected exceptions → global handler → 500 ProblemDetails.
- **IDs:** `Guid.CreateVersion7()` generated in the application layer; the DB never generates keys.
- **Time:** inject `TimeProvider`; never `DateTime.UtcNow`. Store `timestamptz` in UTC.
- **Persistence:** one `AppDbContext`; each module adds `IEntityTypeConfiguration<T>` classes mapped
  to its own schema (`identity`, `play`, `chat`, `admin`); snake_case names. No stored procedures
  or triggers. Every migration has a working `Down`. Deployments run `dotnet ef database update`
  explicitly — only Development auto-migrates.
- **Real-time:** one SignalR hub per module (`/hubs/play`, `/hubs/chat`). The server is
  authoritative: moves are stored hidden and broadcast only after both are committed; clients never
  decide outcomes.
- **API:** Minimal API endpoints grouped per module under `/api`; OpenAPI at `/openapi/v1.json` in Development.
- **Auth:** ASP.NET Core Identity with cookies; the SPA is same-origin with the API.
- **Frontend:** Tailwind utility classes only (tokens in `web/src/index.css` `@theme`); TanStack
  Query for server state; React Router for routing; no global store until one is needed.
- **Infrastructure:** add no broker, cache or separate service without an ADR.
