# FN41–FN43 — post moderation

Status: implemented and verified 2026-10-08. Worktree admin-foundation, branch feature/admin. Preserved FN44/FN38–FN40. No commit/push or shared Supabase writes.

Accepted flow: new/edited posts remain pending_review while text/images are screened. Gemini chosen by user. Unsafe/uncertain/unscannable results require human review. Auto-publish safe results is an Admin database toggle, default OFF. Admin approve/reject and flagged keep/remove require reasons and an expected content revision. Persist audit and author notification with the decision.

Tasks:
1. Failing real-PostgreSQL HTTP tests for manual moderation, flags, settings, duplicate/stale decisions and notifications.
2. Revision tracking, durable scan outbox/leases, decision history and private RLS migration. Stage scan atomically with post creation/edit. Prevent stale AI/admin decisions from publishing edited/deleted posts.
3. Admin DTO/service/repository APIs and atomic notification publication in ambient transactions.
4. Independent Gemini boundary task: versioned prompt/schema file, constrained multimodal client, tests with fake HTTP (no paid requests). No AI SDK dependency needed for C# HTTP.
5. Hangfire recurring scan dispatcher with bounded retries and lease recovery; fail closed to manual review; idempotent finalization and notification.
6. Full suites, scoped/broad independent review, documentation and configuration examples. Actual key and shared migration remain deployment steps.

Rulings: videos cannot auto-publish unless the provider adapter fully scans them; unsupported media goes to manual review. No FN26 summary/FFmpeg implementation in this phase. API enabled defaults false; queue remains durable for manual review. AI is advisory, never deletes media or posts. Removal hides content and retains evidence.

Verified: all six implementation tasks complete. 181 unit + 232 real-PostgreSQL integration tests passed, no failures/skips. Final results in `.local-data/test-results/phase4-final/`. Pending model changes check clean. Local Hangfire preparation verified with WebPush off and moderation on, no provider request. Independent Gemini-boundary and moderation-workflow reviews found issues corrected with regressions (deprecated provider parameter, nested provider errors, unresolved legacy flags and revision backfill). Real API key smoke test and shared migration remain deployment steps documented in `docs/Member2_Admin_Moderation.md`.
