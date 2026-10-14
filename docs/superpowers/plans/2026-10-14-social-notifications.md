# Social Notifications Implementation Plan

> Execute inline with executing-plans; the user has approved the behavior and requested implementation. No commit/push.

**Goal:** Add BE notifications for likes, follows, root comments, replies, and new posts from selected followed authors.
**Architecture:** Reuse NotificationService and its transactional push outbox. Persist an audience snapshot when creating a public post, deliver in bounded background batches, and recheck eligibility before delivery.
**Tech Stack:** .NET 10, EF Core/Npgsql, PostgreSQL, xUnit/Moq.
**Spec:** User-approved notification flow in the conversation.

## Global Constraints
BE only, use DTOs and existing repositories; work in the D: checkout. No commit/push. New-post preference defaults off and is per followed author. Preserve moderation notifications and notification history. Test writes use isolated local PostgreSQL.

## Review Focus
Self events; concurrent duplicate follows/likes; unlike/refollow spam; preference off before queued delivery; edited/hidden posts and hidden comments.

### Task 1: Root comments, replies, likes
- [x] Observe failing create-comment notification test.
- [x] Extend source projections/publisher and wire visible comment creation and new likes into the same business transaction.
- [x] Verify root vs reply recipient, self exclusion, no unlike events, rollback.

### Task 2: Follow and preference
- [x] Add tests for notifications, default off, ownership and missing relationship.
- [x] Add NotifyOnNewPost to follow status, preference endpoint and serialized follow mutations.
- [x] Verify duplicate follow does not write notifications; per-author updates preserve follow.

### Task 3: New-post durable fan-out
- [x] Test audience capture, batch processing, edits/no duplicate, off/unfollow/hidden suppression and concurrent workers.
- [x] Capture opted-in audience with post creation; process bounded batches transactionally, retain stable event keys; validate pending push source.
- [x] Generate additive migration with RLS/revokes and consistent metadata; test fresh migrations and permission contracts.

### Task 4: Verification and handoff
- [x] Run full unit tests, focused PostgreSQL integration tests and API build.
- [x] Fresh independent code review; resolve findings and repeat affected checks.
- [x] Document new API and deployment status; do not commit or push.

## Execution evidence

- Baseline create-comment notification test failed; then passed after wiring.
- PostgreSQL regressions observed failing for hide/reapprove, capture/off/re-enable and stale claimed rows; passed after locks, cancellation trigger and queue recheck.
- Final full suites: 241 unit / 292 integration, zero failures or skips; API build 0 warnings/errors.
- Independent review: initial races resolved; final no blockers.
- User authorized shared migration after tests; applied AddSocialNotifications on approved Supabase project and startup/Swagger/API status succeeded.
- Ruling: use native hosted worker to keep in-app fan-out independent of optional Hangfire/push/Gemini; reuse PostgreSQL locking and existing publisher/outbox.
- No FE modifications, commit or push. HEAD remains e96f7ed on developer.
