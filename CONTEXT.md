---
status: Living
updated_at: "2026-11-14"
---

# Domain Context — Rochambeau

<!--
CONTEXT.md is the domain glossary — not a spec and not a scratch pad. NO implementation
detail here (no datastore/broker/framework names, no API contracts) — only domain words
and the boundaries between them. Implementation choices live in the SAD and ADRs; behaviour
lives in spec.md.
-->

## Glossary

- Account — the registered identity of one person on the portal: a username, an email address, a password and a role. NOT a Profile (the Profile is what is *shown* about an account; the Account is what the portal *knows*).
- Administrator — a Player whose account carries the administrator role and who may run the portal (e.g. moderate users). NOT a separate kind of account — an Administrator also has a username and can play.
- Player — a person with an Account who is logged in to the portal. NOT a Visitor (a Visitor has no account or is not logged in).
- Profile — the page that shows an Account to people: its username, join date and role; the owner additionally sees their own email address. NOT the Account itself — it never exposes the password and shows the email only to its owner.
- Username — the unique, public name a Player is known by to other players (e.g. when challenged to a match). NOT the email address, which is private and used only to log in.
- Visitor — anyone using the portal without being logged in. NOT a Player — a Visitor can only register or log in.

## Invariants

- A username always belongs to exactly one Account; two Accounts can never have usernames that differ only by letter case.
- An email address always belongs to at most one Account.
