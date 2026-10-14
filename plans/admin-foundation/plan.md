# Admin foundation — FN44

Status: complete

Base: local developer 1f3560c. Branch: feature/admin. Do not commit or push.

## Accepted scope

Phase 1 of the agreed backend admin flow: reusable Admin authorization and append-only audit. No member ban APIs, category CRUD, moderation, AI calls, FE changes or shared Supabase migration yet.

## Implementation

1. AdminOnly policy backed by current database role, active/deleted state and temporary login lock. Existing member/guest authentication behavior unchanged. Use the stored lowercase `admin` role.
2. Add `admin_audit_logs`: server-generated UTC timestamp, admin ID, direct peer IP, trace ID, action and target identifiers. Do not copy request bodies, tokens or arbitrary before/after objects into audit records.
3. Service wrapper executes an admin operation and inserts its audit in the same scoped database transaction. Reject nested transactions. Failed operations and audit failures roll back together. Future phase services use this wrapper.
4. Admin-only GET list/detail APIs with bounded pagination and filters. Successful audit trail access is itself audited. No create/update/delete API.
5. PostgreSQL migration: FK without cascade, indexes, RLS/revoked Data API grants, UPDATE/DELETE/TRUNCATE rejection triggers. A privileged database owner can still change schema; this is not external tamper-proof storage.

## Verification

Integration tests with signed JWTs and isolated local PostgreSQL: anonymous/member/stale-role/inactive/deleted/locked identities; real admin access; bounded pagination/filtering; persisted actor/IP/UTC/trace; atomic commit and rollback; SQL mutation rejection; RLS; no mutation API; schema parity. Run existing unit/integration suites. Never use shared Supabase for tests.

Red evidence: the initial anonymous GET test failed with actual 404 versus expected 401 before implementation (`.local-data/test-results/admin-red.trx`).

Verification evidence (2026-10-07):

- `dotnet build tests/VeganHelper.IntegrationTests --no-restore --no-incremental`: passed, 0 warnings/errors.
- `dotnet test tests/VeganHelper.UnitTests --no-restore`: 108 passed, 0 failed/skipped, including model/snapshot parity.
- `dotnet test tests/VeganHelper.IntegrationTests --no-restore`: 106 passed, 0 failed/skipped (29 Admin foundation cases and 77 existing cases).
- PostgreSQL 17.11, isolated loopback test databases at 127.0.0.1:55439; final fresh API database `member2_test_admin_foundation_icu` (ICU en-US), repository database `veganhelper_test_developer_merge`; no shared Supabase writes.
- `dotnet ef migrations has-pending-model-changes --project src/VeganHelper.DAL --no-build`: no pending model changes. Installed EF CLI 10.0.11 reports a version notice against runtime 10.0.12; generation and parity checks succeeded.
- `git diff --check`: passed. Historical SQL Server snapshot in this worktree restored to base; user's modified snapshot in the original Sprint 2 worktree remains untouched.
- Final TRX evidence: `.local-data/test-results/all-unit.trx`, `.local-data/test-results/all-integration-final.trx`.

Resolved verification issues: EF CLI selected the excluded historical snapshot path; relocated its generated content into the active PostgreSQL snapshot and rebuilt (copying preserved the older timestamp). A deleted-user test fixture initially violated the existing `deleted_at IS NULL OR is_active = false` constraint; corrected only that fixture.

Read-only independent review: no Critical/Important production findings. The identified verification gap (browser roles absent in a fresh container could make conditional REVOKE assertions vacuous) was closed: fixture now sets up both roles and representative default grants before migration; audit privilege checks require both roles and check sequence grants too. Follow-up review accepted the fixture changes.

A fresh local database initially inherited plain C locale; the existing `DeclareAllergies_WhenTwoUsersCreateSameCustomNameConcurrently_ReusesOneIngredient` test failed (105/106 integration cases passed). Diagnosed environment/setup issue, not an Admin regression: PostgreSQL `lower` on the same Vietnamese uppercase literal differed between C and ICU en-US (false vs true for the expected lowercase value). Created a fresh ICU en-US database, matching the previously verified locale, and all 106 integration cases passed. Preserved the failed run in `all-integration.trx`; no allergy production code or assertions changed.

No commit, push, shared Supabase migration, FE modification or Admin account promotion was performed. Original Sprint 2 working changes remain untouched.
