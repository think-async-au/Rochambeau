---
status: Accepted
owner: "a.udalov"
reviewers: []
updated_at: "2026-11-13"
feature_size: "n/a (foundation)"
ticket: "docs/idea-brief.md"
---

# 0001 — Use .NET 10 + ASP.NET Core + SignalR + PostgreSQL with a React + TypeScript SPA

- **Status:** Accepted
- **Date:** 2026-11-13
- **Deciders:** a.udalov (owner), during the `survey` greenfield foundation session

## Context

Rochambeau is a web-first, real-time rock-paper-scissors portal. Its first version covers live 1v1 matches, in-game chat and minimal admin; tournaments, leagues and a forum come later. It is a solo learning/portfolio project (see `docs/idea-brief.md`). The repo is empty, so the stack must be chosen before anything else can be scaffolded. Changing it later would mean a rewrite.

## Decision drivers

- The owner's existing skills are .NET and PostgreSQL (idea brief §5, owner-reported).
- Real-time head-to-head play needs bidirectional push. The owner suggested SignalR.
- Capacity is solo, 1–2 h/day (idea brief §5), so the stack should use mainstream, well-documented tools.
- Portfolio value: the owner chose a frontend skill that is widely in demand.
- Desktop and mobile clients come later (idea brief §5), so the server API must stay client-agnostic.

## Considered options

1. **ASP.NET Core + SignalR + PostgreSQL, with a React + TypeScript (Vite) SPA.**
2. **ASP.NET Core + SignalR + PostgreSQL, with a Blazor WebAssembly frontend.** Everything is C#, and MAUI Blazor Hybrid could reuse the components for desktop and mobile.
3. **Stay on .NET 9** (the SDK already installed) instead of .NET 10.

## Decision outcome

**Chosen:** Option 1 on **.NET 10 (LTS)**. The backend matches the owner's stack, and SignalR covers real-time play. The owner picked React + TypeScript over Blazor because it is more valuable in a portfolio, and accepted maintaining two languages in exchange. .NET 10 was chosen over .NET 9 because .NET 9 is a short-term-support release whose support ends around November 2026, while .NET 10 is LTS (supported until November 2028).

## Consequences

**Positive**
- The backend uses tools the owner already knows. SignalR has an official TypeScript client (`@microsoft/signalr`).
- The REST and SignalR API is consumed by a separate SPA, so future desktop and mobile clients plug into the same API.
- React + TypeScript adds visible frontend skill to the portfolio.

**Negative**
- There are two languages and two toolchains (dotnet and npm). DTOs must be kept in sync by hand or by generating them from OpenAPI.
- New prerequisites: the .NET 10 SDK and Node.js LTS (neither is installed today).

**Neutral**
- Desktop and mobile later would use React Native or Electron/Tauri rather than reusing Blazor components. That is decided when the roadmap reaches that step.

## Links

- Idea brief: [[../idea-brief.md]] §5, §7
- Architecture map: [[../architecture-map.md]] §Stack
- Related ADR: [[0002-use-modular-monolith-with-feature-folders]], [[0003-use-ef-core-single-dbcontext-schema-per-module]]
