---
id: T27
title: "Keep the Player view and show an error when log-out fails"
layer: "ui"
deps: ["T26"]
blocks: []
acs: ["AC-07"]
files_hint: ["web/src/features/auth/LogoutButton.tsx", "web/src/features/auth/RequireAuth.test.tsx"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14 F9"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F9); the source always wins. -->

# T27 — Keep the Player view and show an error when log-out fails

## Why (review finding)

`LogoutButton.tsx:14-18` clears `me` and navigates in `onSettled`, also after a 500 or network failure, while the HttpOnly cookie stays valid — the next user of the browser can open the previous owner's Profile.

— `_review/review-2026-11-14.md`, F9 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

RTL tests: DELETE 500 → still the Player view + "Could not log out. Please try again."; 401 → treated as logged out. Web test, lint, build clean.