---
id: T29
title: "Move focus to the first invalid field after a failed submit"
layer: "ui"
deps: ["T28"]
blocks: []
acs: ["AC-02", "AC-03"]
files_hint: ["web/src/features/auth/RegisterPage.tsx", "web/src/features/auth/LoginPage.tsx", "web/src/shared/ui/TextField.tsx", "web/src/features/auth/RegisterPage.test.tsx", "web/src/features/auth/LoginPage.test.tsx"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14 F14"
status: "todo"
step: "—"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F14); the source always wins. -->

# T29 — Move focus to the first invalid field after a failed submit

## Why (review finding)

On 400/409 the field error is linked only via `aria-describedby`; focus stays on the submit button and nothing is announced (`RegisterPage.tsx:38-50`, `LoginPage.tsx:32-44`, `TextField.tsx:28-32`).

— `_review/review-2026-11-14.md`, F14 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

RTL test: after a 400 with field errors, focus is on the first `aria-invalid="true"` input (Register and Log in). Web test, lint, build clean.