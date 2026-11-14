---
id: T28
title: "Offer Register and Log-in links on the landing page"
layer: "ui"
deps: ["T27"]
blocks: []
acs: ["AC-01", "AC-07"]
files_hint: ["web/src/App.tsx", "web/src/features/home/"]
owner: "a.udalov"
estimate: "XS"
origin: "review-2026-11-14 F12"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F12); the source always wins. -->

# T28 — Offer Register and Log-in links on the landing page

## Why (review finding)

`App.tsx:8-15` renders only a title on `/` (the first-visit page and where log-out lands); a Visitor can reach Register / Log in only by typing the URL (spec §2 goal, §7 KPI).

— `_review/review-2026-11-14.md`, F12 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

RTL test: a Visitor sees "Create account" and "Log in" links; a Player sees "My profile". Web test, lint, build clean.