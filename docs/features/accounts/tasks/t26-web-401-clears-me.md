---
id: T26
title: "Treat any 401 from an authenticated query as logged out, and keep me fresh briefly"
layer: "ui"
deps: ["T18"]
blocks: []
acs: ["AC-08", "AC-10", "AC-13b"]
files_hint: ["web/src/main.tsx", "web/src/shared/api/", "web/src/features/profile/ProfilePages.test.tsx"]
owner: "a.udalov"
estimate: "S"
origin: "review-2026-11-14 F3, F13"
status: "done"
step: "COMMIT"
---

<!-- Follow-up task added by implement from the review record. The finding is quoted from
`_review/review-2026-11-14.md` (F3, F13); the source always wins. -->

# T26 — Treat any 401 from an authenticated query as logged out, and keep me fresh briefly

## Why (review finding)

F3: when `GET /api/v1/profiles/{u}` answers 401 (expired or revoked session) `PublicProfilePage.tsx:18-23` shows "Could not load this profile" and the cached `me` stays a Player; sad §6 says the page asks the Player to log in again. F13: `me.ts:21-28` says `setMe` avoids a `/me` round trip, but `staleTime: 0` refetches immediately.

— `_review/review-2026-11-14.md`, F3, F13 · full text: [review](../_review/review-2026-11-14.md)

## Definition of Done

RTL test: `me` 200 + profile 401 → "Log in or register first." is shown. A 401 from any query clears `me` (global `QueryCache` `onError`). The `me` query has a short `staleTime` so the `setMe` comment holds. `npm --prefix web test`, `lint`, `build` clean.