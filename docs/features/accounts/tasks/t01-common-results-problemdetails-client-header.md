---
id: T1
title: "Add Result/Error, the ProblemDetails mapping and the client-header filter to Common"
layer: "ports"
deps: []
blocks: ["T4"]
acs: ["AC-02"]
files_hint: ["src/Rochambeau.Api/Common/Results/", "src/Rochambeau.Api/Common/Http/", "src/Rochambeau.Api/Program.cs", "tests/Rochambeau.UnitTests/Common/", "tests/Rochambeau.IntegrationTests/Host/"]
owner: "a.udalov"
estimate: "M"
context_budget: "S"   # measured: 39 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T1 — Add Result/Error, the ProblemDetails mapping and the client-header filter to Common

## Place in the sequence

- **Blocked by:** — (none)
- **Blocks:** T4 — Wire Identity core, the cookie session and deny-by-default authorization in IdentityModule
- **Wave:** 1 — no prerequisites.
- **Lane:** shares `src/Rochambeau.Api/Program.cs` with T4 and T12 — serialized (already ordered by deps).

## Why (user story)

> **As a** Visitor
> **I want** to register with a username, an email address and a password
> **So that** I become a Player and can use the portal under my own name
>
> — `spec.md §4, US-01, verbatim` · full text: [spec.md](../spec.md)

This task builds the error shape that every AC-02 field error, and every later `errorCode`, travels in. It also adds the CSRF barrier every unsafe `/api` call must pass.

## Inlined context

> **Hard rule:** **Errors:** expected failures return `Result<T>` / `Error` (code, message, type), not exceptions. HTTP maps them to ProblemDetails (RFC 9457) with an `errorCode` extension; hub methods return `HubResult<T>`. Unexpected exceptions → global handler → 500 ProblemDetails.
>
> — `CLAUDE.md §Conventions, Errors, verbatim` · full text: [CLAUDE.md](../../../../CLAUDE.md)

> **Error handling:** `Result<T>` / `Error` (code, message, type) in `Common/Results/`, mapped to ProblemDetails with an `errorCode` extension. Field-level validation failures (AC-02) are returned as ProblemDetails `errors` keyed by field. Identity codes use an `identity.` prefix (e.g. `identity.username_taken`, `identity.invalid_credentials`, `identity.locked_out`, `identity.registration_cap`)
>
> — `sad.md §8, Error handling row, verbatim` · full text: [sad.md](../sad.md)

> **CSRF:** `SameSite=Strict` cookie plus a required `X-Rochambeau-Client` header on every unsafe-method request under `/api` and on SignalR negotiate. No CORS policy is configured
>
> — `sad.md §8, CSRF row, verbatim` · full text: [sad.md](../sad.md)

> - A missing header is rejected before any handler runs, with a ProblemDetails 400 (`errorCode` `common.client_header_required`).
> - Integration tests assert that an unsafe request without the header is rejected even with a valid cookie.
>
> — `adr/0005 §Decision outcome, implementation notes 2–3, verbatim` · full text: [adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md](../adr/0005-require-samesite-strict-cookie-and-client-header-against-csrf.md)

> **Risk:** Brownfield: `Common/` has no `Result<T>` / `Error` or ProblemDetails mapping yet. Accounts defines them, and they become the precedent for every module — **Mitigation:** Keep them minimal (code, message, type → ProblemDetails `errorCode`), review them in the `review` stage, and re-run `/sdd:survey` after this feature so `architecture-map.md` (stale: reflects 079e702) cites the real files
>
> — `sad.md §11, brownfield Result/Error row, abridged` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

No DB changes.

## API contract

- `ProblemDetails`: required `type`, `title`, `status`, `errorCode` (pattern `^[a-z_]+\.[a-z_]+$`, the `module.error_name` form); optional `detail`, `instance`.
- `ValidationProblemDetails` = `ProblemDetails` + required `errors`: an object with one entry per offending request field (`username`, `email`, `password`), each a list of strings saying what the field must be (AC-02).
- `X-Rochambeau-Client` header: required on every unsafe request under `/api`. When it is missing, the request is rejected before any handler runs with `400`, `errorCode` `common.client_header_required`, title "The X-Rochambeau-Client header is required.". The value is not interpreted; the web client sends `web`.

— `contracts/openapi.yaml §components, ProblemDetails + ValidationProblemDetails + ClientHeader, abridged` · full text: [openapi.yaml](../contracts/openapi.yaml)

## Acceptance criteria

### AC-02 — error

> **Given** a Visitor on the registration form
> **When** the Visitor submits a username shorter than 3 or longer than 20 characters, or with characters other than letters, digits, underscore or hyphen, or an email address that is not well formed, or a password shorter than 8 or longer than 128 characters
> **Then** the system creates no Account, and shows next to each offending field what it must be (for example "the password must be at least 8 characters"), keeping what the Visitor typed except the password. "Letters" means the Latin letters a–z and A–Z only, so names that only look alike (e.g. with a Cyrillic "о") cannot be registered. Spaces at the start or end of the username and email are trimmed before checking. Length is the only password rule: no required digits, capitals or symbols
>
> — `spec.md §5, AC-02, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] `Result`, `Result<T>`, `Error(code, message, type)` and an `ErrorType` (at least validation, conflict, not-found, unauthorized, locked, too-many-requests). A validation error carries a field → messages map. Plain C# with no ASP.NET types, so Domain/Application can use it — `src/Rochambeau.Api/Common/Results/`
- [ ] Map `Error` → `IResult`: a ProblemDetails with the `errorCode` extension and a status chosen by `ErrorType`. A validation error becomes a ValidationProblemDetails with `errors` keyed by field — `src/Rochambeau.Api/Common/Http/`
- [ ] A client-header filter (middleware). Any unsafe method (POST/PUT/PATCH/DELETE) under `/api`, and any SignalR negotiate under `/hubs`, without a non-empty `X-Rochambeau-Client` gets `400 common.client_header_required` before routing reaches a handler — `src/Rochambeau.Api/Common/Http/`
- [ ] Register the filter in `Program.cs` with one cross-cutting line, before the endpoint mappings — `src/Rochambeau.Api/Program.cs`
- [ ] Unit tests for the mapping (status, `errorCode`, `errors` keyed by field) — `tests/Rochambeau.UnitTests/Common/`
- [ ] Integration test: an unsafe `/api` request without the header gets `400` + `common.client_header_required`, while `GET /health` is unaffected — `tests/Rochambeau.IntegrationTests/Host/`

## Edge cases

| Case | Behaviour |
|---|---|
| Header present but empty / whitespace | `400 common.client_header_required` (ADR-0005: "missing" = no usable value) |
| Header with any non-empty value | Accepted — the value is not interpreted |
| `GET` / `HEAD` without the header | Passes. Only unsafe methods are checked; the WebSocket upgrade is a GET (ADR-0005) |
| Several invalid fields at once | One ValidationProblemDetails with one `errors` entry per field, not one response per field |
| Unexpected exception in a handler | Unchanged: the global handler returns a 500 ProblemDetails (CLAUDE.md · Errors) |

## Definition of Done

- [ ] Unit tests for `Error` → ProblemDetails mapping pass (status, `errorCode`, field-keyed `errors`)
- [ ] Integration test: unsafe `/api` request without `X-Rochambeau-Client` → `400 common.client_header_required`; `GET /health` still `200`
- [ ] `Common/` references no Identity or module type
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
