---
status: Accepted
owner: "a.udalov"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-14"
feature_size: "M"
ticket: "docs/features/accounts/spec.md"
---

# 0003 — Store exactly one role per Account in a role column

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** a.udalov (owner), during the `design` Socratic walk for accounts

## Context

The glossary defines an Account as «a username, an email address, a password and a role». Today there are two roles, Player and Administrator. A Moderator role arrives with the forum (roadmap step 8), and the target architecture has a separate user service with roles and permissions (`target-architecture.md`). Admin (step 6) and Forum (step 8) will read the role through the identity contract. The storage shape has to be fixed with the first migration (SAD §4).

## Decision drivers

- The glossary and the spec speak of one role per Account. A Profile shows «the role» (CONTEXT.md, AC-09, AC-11, AC-13).
- Privilege escalation must be impossible through the portal (AC-14): fewer writable places means fewer ways in.
- A role change must end every session (AC-13b), which works through the security stamp (accounts ADR-0002).
- Keep the door open for a Moderator role and finer permissions without building them now (spec §3).

## Considered options

1. **One `role` column on the account row.** Values `player` | `administrator` (`moderator` added later). A custom claims-principal factory emits it as a standard role claim.
2. **Identity's role tables.** `AddRoles<IdentityRole<Guid>>()` with `identity.roles` + `identity.user_roles` (many-to-many). Player is assigned at registration and Administrator is added by the operator.

## Decision outcome

**Chosen:** Option 1. It matches the domain language exactly (one role per Account) and gives the Profile one value to show. It also leaves a single column, written only by the operator command, as the escalation surface for AC-14. Option 2 is the framework's standard and supports several roles per person. But it adds two tables and a join every time claims are rebuilt (on every request, per accounts ADR-0002), and it stores a redundant «Player» row for every Account.

Implementation notes that follow from this choice:
- Registration always writes `player`. No HTTP endpoint or hub method accepts a role value (AC-14).
- The operator command (SAD §5) is the only writer of `role`. It sets the new value and rotates the security stamp in the same save (AC-13b).
- The role is exposed to other modules as part of the identity contract (accounts ADR-0004), never as a raw claim name.
- The hierarchy (an Administrator can do everything a Player can, and later everything a Moderator can) lives in application code, not in the data.

## Consequences

**Positive**
- Simple to read and test: one value, no join, and exactly one role on every Profile.
- A tiny escalation surface. One write path, outside the portal.

**Negative**
- A person cannot hold two independent roles at once. The hierarchy has to be coded.
- Fine-grained permissions (target «user service») will need a migration to a role/permission table, under its own ADR.

**Neutral**
- `RequireRole("Administrator")`-style policies work unchanged, because the value is emitted as a standard role claim.

## Links

- Spec: [[../spec.md]] AC-01, AC-09, AC-11, AC-13, AC-13b, AC-14, §3 «Two-factor login and a Moderator role»
- SAD: [[../sad.md]] §4 (choice 3), §5
- Related ADR: [[0002-validate-cookie-security-stamp-on-every-request]]
