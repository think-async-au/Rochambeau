---
id: T12
title: "Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow"
layer: "app"
deps: ["T10"]
blocks: ["T13"]
acs: ["AC-13", "AC-13b", "AC-14"]
files_hint: ["src/Rochambeau.Api/Common/Commands/", "src/Rochambeau.Api/Program.cs", "src/Rochambeau.Api/Modules/Identity/Application/ChangeRole/", "src/Rochambeau.Api/Modules/Identity/Commands/", "src/Rochambeau.Api/Modules/Identity/IdentityModule.cs", "tests/Rochambeau.UnitTests/Common/", "tests/Rochambeau.IntegrationTests/Identity/", "CLAUDE.md", "docs/architecture-map.md"]
owner: "a.udalov"
estimate: "M"
context_budget: "M"   # measured: 53 inlined lines
status: "todo"
step: ""
---

<!-- Self-contained task: the slice this task needs is inlined below, each chunk with a provenance
signature (`<file> §<section>, <identifier>, verbatim|abridged`). An inline is a snapshot taken at
breakdown time (2026-11-14); the source always wins.
To the executing agent: work from what is inlined here. If a slice is insufficient, ambiguous, or
contradicts the code in front of you, open the named file for the full text and follow that. Do not
invent the missing part. -->

# T12 — Add the operator-command hook and the grant-admin / revoke-admin ChangeRole flow

## Place in the sequence

- **Blocked by:** T10 — Serve GET /api/v1/me and log out with DELETE /api/v1/session
- **Blocks:** T13 — Revalidate the session on every hub invocation with SessionRevalidationHubFilter
- **Wave:** 8 — starts once T10 is done.
- **Lane:** shares `Program.cs` with T1/T4 and `src/Rochambeau.Api/Modules/Identity/IdentityModule.cs` with T4–T13 — serialized.

## Why (user story)

> **As an** Administrator
> **I want** the portal to recognise my administrator role when I log in
> **So that** moderation tools can be offered to me, and only to me, once they exist
>
> — `spec.md §4, US-06, verbatim` · full text: [spec.md](../spec.md)

This task lets the portal's operator, and only the operator, grant or remove the administrator role. The change ends the Account's sessions everywhere.

## Inlined context

> Decision override: module layers gain a `Commands/` entry adapter (next to Endpoints / Hubs), and `Program.cs` gains one generic operator-command dispatch line — rationale: AC-13/AC-14 require the administrator role to be granted outside the portal by the operator, through the same handler that rotates the security stamp. A generic hook in `Common/Commands/` keeps `Program.cs` free of module-specific code, which is the intent of `CLAUDE.md` · Layout. `CLAUDE.md` and `architecture-map.md` are updated to list the new layer (§11).
>
> — `sad.md §1, Decision overrides, verbatim` · full text: [sad.md](../sad.md)

> **Hard rule:** **Operator command.** The operator command (`grant-admin <username>` / `revoke-admin <username>`) is the only writer of the role (accounts ADR-0003). A generic, module-agnostic operator-command mechanism lives in `Common/Commands/`. Each module registers its commands in its `Add<Name>Module`, and `Program.cs` gains one non-module-specific line before it starts the web host: «if the arguments name a registered command, run it and exit». When `grant-admin` or `revoke-admin` is given, the Identity module runs it through the same `ChangeRole` handler, which sets the role and rotates the security stamp (AC-13, AC-13b). The process then exits without serving HTTP. No endpoint can reach that handler (AC-14).
>
> — `sad.md §5, Operator command, verbatim` · full text: [sad.md](../sad.md)

> Operator flow: look up the Account by normalised username → none: "no such account, nothing changed", exit with a failure code → already has the requested role: "role unchanged, sessions kept", exit → differs: set the new role and rotate the security stamp **in one write**, log "role changed" keyed by account id, report success, exit without serving the portal.
>
> — `sad.md §6, Operator grants or removes the administrator role, abridged` · full text: [sad.md](../sad.md)

> **Hard rule:** The operator command (SAD §5) is the only writer of `role`. It sets the new value and rotates the security stamp in the same save (AC-13b).
>
> — `adr/0003 §Decision outcome, implementation note 2, verbatim` · full text: [adr/0003-store-one-role-per-account-in-a-column.md](../adr/0003-store-one-role-per-account-in-a-column.md)

> **Risk:** Convention drift: `CLAUDE.md` · Layout and `architecture-map.md` don't yet list the `Commands/` layer or the generic operator-command hook (§1 override) — **Mitigation:** Update both in the same change that adds `Common/Commands/` (`implement`), or at the `/sdd:survey` re-run
>
> — `sad.md §11, Convention drift row, verbatim` · full text: [sad.md](../sad.md)

**Fallback:** if a slice is insufficient or contradicted by the code, read the named file in full ([spec.md](../spec.md) · [sad.md](../sad.md) · [data-model.md](../data-model.md) · [openapi.yaml](../contracts/openapi.yaml) · [adr/](../adr/)) and follow it. Do not guess.

## Data delta

| Column | Type | Constraints | Change |
|---|---|---|---|
| `normalized_user_name` | `varchar(20)` | NOT NULL, UNIQUE (`ix_accounts_normalized_user_name`) | read (unique lookup) |
| `role` | `varchar(20)` | NOT NULL, CHECK `ck_accounts_role` | written (one save) |
| `security_stamp` | `text` | NOT NULL | written (one save) |

No schema change.

— `data-model.md §Entities, table identity.accounts, abridged` · full text: [data-model.md](../data-model.md)

## API contract

Internal — no API surface.

## Acceptance criteria

### AC-13 — happy path

> **Given** an Account that the portal's operator has designated as an administrator
> **When** its owner logs in
> **Then** the system recognises them as an Administrator and their Profile shows the Administrator role
>
> — `spec.md §5, AC-13, verbatim` · full text: [spec.md](../spec.md)

### AC-13b — authorization, role change

> **Given** an Account that is logged in on one or more devices
> **When** the portal's operator grants or removes the administrator role on that Account
> **Then** the system ends that Account's sessions on every device, so the owner must log in again and from then on is recognised with the new role. Until then, no device keeps acting with the old role. Removing the administrator role this way is how an Administrator loses their powers; a ban (roadmap step 6) must end sessions on every device in the same way
>
> — `spec.md §5, AC-13b, verbatim` · full text: [spec.md](../spec.md)

### AC-14 — authorization

> **Given** a Visitor registering, or a logged-in Player
> **When** they try to give themselves, or anyone else, the administrator role through the portal
> **Then** the system offers no way to do so, and any attempt leaves every Account's role unchanged
>
> — `spec.md §5, AC-14, verbatim` · full text: [spec.md](../spec.md)

## Checklist

- [ ] Generic, module-agnostic operator commands: an `IOperatorCommand` (name, run → exit code), a registry, and `AddOperatorCommand<T>()` — `Common/Commands/`
- [ ] `Program.cs`: one line before `RunAsync` — "if the arguments name a registered command, run it and exit". No module-specific code — `Program.cs`
- [ ] ChangeRole handler: the branches above. Set `Role`, then `UserManager.UpdateSecurityStampAsync` in the same save — `Application/ChangeRole/`
- [ ] `grant-admin <username>` / `revoke-admin <username>` commands registered in `AddIdentityModule`. Map the result to exit codes and console messages — `Commands/`, `IdentityModule.cs`
- [ ] Test helper `PromoteToAdministratorAsync(services, username)` that runs the ChangeRole handler and never writes `role` directly — `tests/Rochambeau.IntegrationTests/Identity/`
- [ ] Tests: registry dispatch (unit); AC-13 (promote → log in → `/me` shows `administrator`); AC-13b (two logged-in clients → change role → both `/me` `401` → log in again → new role); same-role no-op keeps sessions; unknown username fails; AC-14 (the set of mapped `/api` routes is exactly the five contract operations, and none accepts a role) — `tests/Rochambeau.UnitTests/Common/`, `tests/Rochambeau.IntegrationTests/Identity/`
- [ ] Docs: add the `Commands/` layer and `Common/Commands/` to `CLAUDE.md` · Layout and to `docs/architecture-map.md` — `CLAUDE.md`, `docs/architecture-map.md`

## Edge cases

| Case | Behaviour |
|---|---|
| `grant-admin` for an unknown username | Error message, non-zero exit code, nothing changed |
| `grant-admin` for an Account that is already an administrator | "nothing to do", exit 0, sessions kept (sad §6 flag) |
| Arguments name no registered command | The web host starts normally |
| Role changed while 2 devices are logged in | Both get `401` on their next HTTP request (hub side → T13) |

## Definition of Done

- [ ] Integration tests for AC-13, AC-13b (HTTP side) and AC-14 pass; the registry dispatch unit test passes
- [ ] Running the API executable with `grant-admin <username>` changes the role and exits without serving HTTP (manual check noted in the PR)
- [ ] `CLAUDE.md` and `docs/architecture-map.md` list the `Commands/` layer
- [ ] every Hard Rule inlined above still holds
- [ ] gate green: `dotnet build` (warnings are errors) + tests + `dotnet format --verify-no-changes`; for `web/`: `npm --prefix web test` + `lint`
