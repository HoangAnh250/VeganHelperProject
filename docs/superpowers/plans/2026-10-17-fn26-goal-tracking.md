# FN26 Goal Tracking Implementation Plan

**Goal:** Extend the existing weekly menu with safely screened weight goals, dated adherence tracking, reminders, three-day reviews and confirmed future revisions.

**Architecture:** Preserve Controller → BLL DTO/service → DAL repository. Reuse the deterministic generator, snapshot persistence and notification outbox. Use PostgreSQL transactions and Hangfire; keep payment behind an injectable access policy until the payment module exists.

**Constraints:** English UI and API copy; one dish per meal; no image work; no commits/pushes; no shared database migration without final authorization. Existing maintenance requests remain compatible.

**Safety policy:** Product guardrails, not a clinical guarantee: adults 20–65 who explicitly confirm no pregnancy/breastfeeding or condition requiring clinician-managed nutrition; target BMI 18.5–24.9; maximum adjustment 500 kcal/day and 20% of TDEE; maximum change 0.5 kg/week; minimum intake 1500 male / 1200 female. Screen both proposed target and generated daily totals. Use the same evaluator for create, preview, confirm and continuation; never clamp unsafe goals. The 7700 kcal/kg model is an estimate, not an achievement guarantee. Document sources and policy version.

## Review focus
- Unsafe or shortened remaining timelines cannot trigger catch-up restriction.
- Unlogged meals never count as skipped; reminders and reviews are idempotent.
- Owner isolation, simultaneous revision confirmation, and stale profile/proposal rejection.
- Local dates/timezones and day boundaries; only elapsed meal slots can be logged.
- Preserve past menu entries and logs on revisions; old snapshots remain readable.

## Tasks
- [x] 1. Tests and shared safety evaluator: safe loss/gain, unsafe targets/rates, deadline, profile change and generated-day checks.
- [x] 2. Persistence and APIs: routine settings, dated meal logs, preview/confirm revision and next week, ownership/version checks, PostgreSQL migration with RLS.
- [x] 3. Hangfire routine sweep: due reminders, three-day review with sufficient logging and unique event keys; use existing notification outbox.
- [x] 4. FE integration: activate weight goal form, explicit eligibility confirmation, timezone/reminder settings, tracking, progress and server preview/confirm flow using current layout.
- [x] 5. Regression/unit/integration/browser checks, migration SQL review and API/runbook documentation. Record actual results here.

## Decisions
- Payment implementation remains owned by the teammate. Access policy is injectable and configuration defaults preserve existing access; do not claim payment enforcement until connected.
- A goal spans linked weekly plans. A revision replaces only future dates in its own week and preserves elapsed days. Continuing creates the next seven-day window, reruns safety and stops at the goal date.
- A missing tracking log is not nonadherence. Each completed three-day window requires at least six logged slots; below 70% followed generates a profile-review recommendation. These are product thresholds.
- Preview never mutates a plan; confirmation rechecks the profile fingerprint and source revision under a row lock before regenerating.

## Verification log
- Final unit suite: 269 passed. Final focused PostgreSQL FN26 suite: 14 passed, including concurrent confirmation, copied historical logs, cross-week positive reviews, RLS privileges, reminder idempotency, continuation and stale profile rejection.
- Broader integration run before final timezone/locking/queue hardening: 311 passed. FN26-focused tests were rerun after the service changes; all 14 passed.
- FE full suite after timezone fixes: 23 files / 202 tests passed; production build passed (existing large-chunk warning).
- Isolated Chrome fixture flow passed: goal safety → create → track → reject unsafe revision; desktop/tablet/mobile 1440,768,390,320, no JS errors or horizontal overflow.
- Independent reviewer found a Windows/IANA timezone mismatch and continuation edge cases; both fixed with regressions. Reviewed payment noop and images as intentionally outside this implementation.
- Ruling: retain current-week tracking/reminders when planning the next week early. `continued` freezes further successor creation but leaves logs and reminders available. Revisions become `superseded`. Root/date event keys deduplicate cross-version notifications.
- Ruling: use the health repository's existing per-user advisory lock during generation/confirmation to prevent profile/allergy writes racing snapshot validation and persistence.
- EF tooling initially wrote its snapshot into the excluded legacy directory; restored that file from HEAD and generated the compiled PostgreSQL snapshot from the new migration target model. Model tests pass.
- Shared Supabase migration and local worker flag applied only after explicit user approval. Read-only post-check: RLS enabled, anon/authenticated denied, no pending migrations, Hangfire prepared. BE restart required to activate changed source.
