# FN38–FN39

Status: complete. Branch: feature/admin. Preserved uncommitted FN44. No commit/push.

1. Failing API and security tests on isolated PostgreSQL.
2. Admin-only paginated search/filter and member details via DTO/service/repository.
3. Ban history, required reasons, optional future expiry; protect self/admin accounts. Ban is separate from activation/login lock.
4. Serialize target mutations; atomically commit ban/unban, refresh revocation, token version increment and audit.
5. Validate session version and current account state on every authenticated API. Reject old sessions after unban/expiry; require fresh login.
6. Full regression suites, independent review, API/rollout documentation. Shared Supabase migration is a separate deployment step.

## Verification (2026-10-07)

- Initial red run: 15 API tests failed because endpoints/version validation were absent.
- Final `dotnet test`: 108 unit + 135 integration passed, 0 failures/skips, isolated real PostgreSQL 17 with ICU locale.
- Explicit non-incremental build: 0 warnings/errors. Whole-model snapshot parity test passed; EF reports no pending model changes (CLI emits an existing 10.0.11 vs runtime 10.0.12 version notice).
- Independent read-only review: no actionable production findings; outdated phase1 docs updated.
- `git diff --check` clean; excluded SQL Server snapshot unchanged. Existing FN44 working changes retained.
- Evidence: `.local-data/test-results/admin-members-final_net10.0_20261007151426.trx` (unit), `admin-members-final_net10.0_20261007151445.trx` (integration).
- Migration applied only to isolated test databases. Supabase shared deployment pending per rollout docs; no shared database writes or Git publication.
