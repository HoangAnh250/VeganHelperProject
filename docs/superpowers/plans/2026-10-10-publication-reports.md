# Publication and Reports Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task-by-task; user requested execution in this session. No commits or pushes.

**Goal:** Publish new posts and normal edits immediately, perform background checks, hold flagged content for Admin, and support user reports.

**Architecture:** Preserve Controller → BLL DTO/service/AutoMapper → DAL repository/PostgreSQL. Persist a moderation hold independently of content revision; atomically save flags, state and notifications. Reuse flags for user reports with immutable reported-content snapshots and append-only decisions.

**Tech Stack:** .NET 10, EF Core/Npgsql, PostgreSQL, Hangfire, Gemini REST, React 18/CRA, Axios.

**Spec:** ../specs/2026-10-10-post-publication-background-check-design.md

## Global Constraints

- Admin alone releases held content; editing and safe AI results cannot bypass holds.
- Reports do not automatically hide content; reporter identity is not returned to public/author DTOs.
- Quota errors are operational, not violations. Shared cooldown and deferred retry must survive process restarts.
- No Supabase writes, real AI calls, commits or pushes. Preserve excluded SQL Server snapshot.

## Review Focus

- Edit/AI hold race: database lock/revision checks must prevent stale writes clearing a hold.
- Reports against an old revision: preserve snapshot, reject stale submissions, require current revision for decisions.
- Report spam/concurrency: pending dedupe and per-account rate limit must be enforced under database locks.
- Rechecking safe content after report: coalesce work, preserve prior result history, never reset retries for each report.
- Quota outages: do not spend every post's retries while provider is exhausted, do not starve ordinary scans indefinitely.

## Task 1 — Immediate publication and durable hold

Files: Post.cs, PostService.cs, PostRepository.cs/IPostRepository.cs, Post DTOs/mapping, PostModerationModelConfiguration.cs, new Postgres migration; publication regression tests.

Interfaces: Post.ModerationHold bool; DAL LockPostPublicationStateAsync(id, expectedRevision) returns current status/hold under transaction lock.

- [x] Add/run failing HTTP PostgreSQL tests for new published posts, normal published edits, held edits, stale edit/hold race and legacy migration preservation.
- [x] Implement published creates, lock-time hold preservation and pending held edits. Return status/revision to FE; detail includes contentRevision.
- [x] Generate new PostgreSQL migration, backfill hold for legacy pending/rejected/hidden without publishing them, preserve original excluded snapshot.

## Task 2 — Background checking and quota recovery

Files: PostModerationProcessor.cs, ModerationRepository.cs, moderation client/contracts, settings/scan entities/config, NotificationService/contract; processor tests.

Interfaces: NotifyModerationHoldAsync(postId, revision, reason); shared settings.ProviderRetryAfter timestamp; scan.Priority and scan.LastReportRevision tracking; persisted assessment history for report-triggered rechecks.

- [x] Test published safe/flagged, held safe, retry 429/cooldown and reset recovery, stale/deleted/human-reviewed finalization.
- [x] Claim published and pending scans; safe does not publish held content. Flagged transaction sets hold/pending plus one notification. Retire auto-publish settings mutation explicitly.
- [x] Parse quota errors without exposing response text; defer 429 using shared cooldown, retain bounded retries for other transient errors.

## Task 3 — Member report and Admin decisions

Files: PostReportDtos.cs, IPostReportService/PostReportService.cs, IPostReportRepository/PostReportRepository.cs, report controllers, mapping, migrations, report tests.

Interfaces: ReportAsync(userId, postId, CreatePostReportRequest, ct); ListAsync/GetAsync(AdminActor,...); DecideAsync(AdminActor,id,ModerationDecisionRequest,ct).

- [x] Test login/ownership/reason/revision, pending duplicate concurrency, rate limits (10 reports/hour/user), snapshot/privacy, priority recheck after safe, no hide on report/AI failure.
- [x] Add repeated-report coalescing and reported snapshot. Admin dismiss resolves only target report and leaves hold unchanged; remove hides post with hold. Admin keep of held post resolves related pending flags with explicit reviewed revision.
- [x] Verify shared audit transaction/rollback coverage; test concurrent decisions, stale revision, preserved older evidence and hidden detail access.

## Task 4 — React publication/report/notification integration

Files: postService/authPostRegression.test, CreatePost/EditPost/MyPosts, PostDetailCore + report modal/service, notification service/dropdown, small Admin report page/routes/service.

- [x] Test report payload/revision, repeated media fields, publication response status and notification read/badge; inspect report UI API error handling.
- [x] Show immediate publication messages, held-edit message from server, status in My Posts; report action only for logged-in non-author, modal with reason and error handling.
- [x] Replace mock notification dropdown with live list/badge/read polling; Admin page lists reports, original snapshot/current content, expectedRevision decisions without exposing reporter outside Admin.

## Task 5 — Full verification and review

- [x] Run BE unit/integration on isolated PostgreSQL, model parity/migration regression, FE Jest and production build.
- [x] Request independent read-only review of changed behavior; fix verified blockers and rerun relevant tests.
- [x] Update implementation/config docs; report exact checks/limitations, migration not applied to Supabase and no Git publication.

## Completion evidence — 10 October 2026

Tasks 1–5 completed inline, uncommitted. Follow-up user requirement applied: compact Gemini v2 contract contains only PASS/FAIL and reason (max 200 characters), nullable confidence, generic post-level finding, low thinking and no summaries. Existing Admin DTO/history remains compatible. Output/schema/API failures cannot become PASS or evidence of a violation.

Verification: dotnet test VeganHelper.sln --no-restore --verbosity quiet using isolated local PostgreSQL: 181 unit + 256 integration passed, zero failures/skips. Results under .local-data/test-results/compact-verified. FE: 14 Jest tests passed and production build succeeded (unchanged by compact contract). EF model parity and upgrade migration tested. No shared Supabase writes or real Gemini calls.

Independent review: publication/report review findings corrected (profile role, deleted report decisions, assessment provenance, route-change state). Focused follow-up and compact-harness fresh review both returned no actionable findings. User authorization forbids Git publication for this work; no commits/pushes performed. Excluded SQL Server snapshot preserved with original SHA256.

## Scope correction requested by user — 10 October 2026

User assigned FE work to another member. All FE source/service/route/test changes from this implementation were reverted to FE HEAD; the six new files were removed. FE baseline regression suite: 8 tests passed. Earlier Task 4 and 14-test results above describe a superseded implementation, not the current FE. BE implementation preserved, branch renamed feature/reports without commit/push. API contract notes in Member2_Admin_Moderation.md now serve as FE handoff.
