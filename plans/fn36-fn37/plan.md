Status: implemented and verified locally; source integrations and shared deployment pending
Branch: codex/fn36-fn37-notifications
Base: feature/sprint2 at cea0c5c, with a copy of its uncommitted Sprint 2 source and tests.
Original Sprint 2 and FN14 worktrees remain unchanged. No commit, push or merge.

Acceptance: authenticated list/count/read-one/read-all with ownership and idempotent read semantics; browser subscription lifecycle and VAPID config; typed event publisher; atomic notification/push outbox; durable Hangfire worker with leases, bounded retries and expiry; PostgreSQL migration with RLS; isolated real database/API tests and FE contract.
Source feature boundaries: comment CRUD, moderation and FN26 scheduler belong to other modules. They call the publisher after committing their events; this task does not add those APIs.
Deviation: Hangfire PostgreSQL storage follows the user's Supabase requirement instead of the supplied background skill's SQL Server provider.
No shared database migration or real push during tests.

Result: seven authenticated endpoints, three event publisher hooks, PostgreSQL migration/RLS, persisted Hangfire pump and push outbox are implemented. Contract/setup/FE lifecycle: docs/FN36_FN37_Notifications.md.
Verified: 91 unit tests and 68 integration tests passed (0 skipped), including 20 notification API/push cases. Full suite first identified two existing Unicode failures with portable PostgreSQL's C locale; reproduced lower/ILIKE limitation, created fresh ICU en-US test databases and reran the full integration suite without production/test changes to those features.
Evidence: .local-data/fn36-fn37/test-results (unit TRX and initial integration TRX); .local-data/fn36-fn37/test-results-icu (final integration TRX). Tests use loopback-only databases, dummy keys and an HTTP capture handler. Hangfire job persistence is tested without starting a worker or sending push externally.
Final review: subscription ownership prevents taking active endpoints, permits transfer after unsubscribe; repeated reads preserve readAt; stale lease tokens cannot acknowledge renewed work; failed final leases become terminal; SQL Server snapshot remains unchanged in this worktree.
Remaining integration: comment/moderation/FN26 modules must invoke the publisher with committed source events and use a durable source outbox if atomic source-event delivery is required. FE service worker/API hookup, VAPID environment configuration, shared migration and live browser push are not part of the local verification.
