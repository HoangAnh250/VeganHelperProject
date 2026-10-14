# Admin foundation / FN44 (backend)

Branch: `feature/admin`, based on integrated local `developer` (`1f3560c`).

## Scope

This document describes phase 1: database-backed Admin authorization, audit read APIs, and an atomic audit wrapper. The same branch also contains [member management and token revocation (FN38–FN39)](Member2_Admin_Members.md) and [post category management (FN40)](Member2_Admin_Categories.md). No FE work, role-edit endpoints, AI calls, or automatic administrator creation is included.

## Authorization

Apply `[Authorize(Policy = AdminPolicies.AdminOnly)]` to every Admin controller. The policy verifies the JWT through the existing bearer authentication, parses its subject, then checks the current database role (`admin`), `IsActive`, `DeletedAt`, and `LockedUntil`. The database is authoritative; an old JWT containing an admin role cannot bypass a demotion. An expired temporary login lock no longer blocks access.

Responses: anonymous, expired/revoked tokens, missing users, inactive/deleted accounts or active bans → 401; an authenticated user without current admin access → 403. Phase 2 checks the session version and current account state during bearer authentication on all protected routes, and replaces the JWT role with the current database role. JWTs issued before phase 2 lack the required session version: users must log in again after deployment. A failed-password login lock still returns 403 from the Admin policy; it is separate from an administrative ban.

## Audit API

Both endpoints require a current Admin account and a valid access token:

- `GET /api/admin/audit-logs`
  - `PageIndex` (default 1), `PageSize` (default 20, maximum 100).
  - Optional exact filters: `AdminId`, `Action`, `TargetType`, `TargetId`.
  - Optional inclusive `From` / `To` timestamps (ISO 8601 with timezone, e.g. `2026-10-07T00:00:00Z`).
  - Newest first by UTC timestamp, then ID. Uses the existing `PagedResult<T>` contract.
- `GET /api/admin/audit-logs/{id}` → entry, or 404.

Successful list/detail reads create `audit.list` / `audit.view` entries. A list response represents the query before its own read-audit is inserted. Invalid queries, denied access, and missing entries do not create success records. There are no create/update/delete audit HTTP endpoints.

## Writing audit from future Admin features

Services call `IAdminAuditService.ExecuteAsync` around the entire mutation. The callback and audit repository must use the same request-scoped `AppDbContext`. Do not start or commit another transaction in the callback, and do not call the wrapper recursively.

The API constructs `AdminActor` from the authenticated subject, `HttpContext.Connection.RemoteIpAddress` and the server trace identifier. Never bind `AdminActor` from a body or query. `X-Forwarded-For` is not trusted by this implementation; configure trusted proxy handling explicitly at deployment if needed. A missing peer IP (e.g. an in-process test host) is stored as null, not fabricated.

The wrapper rechecks current Admin access, starts a transaction, runs the operation, appends the audit and commits. An operation exception, cancellation or failed audit insert rolls back the transaction. Let failures propagate through the existing exception middleware; dispose the request scope after failure. This wrapper covers database writes only, not irreversible external side effects. Queue notifications/outbox records inside the same transaction and dispatch externally later.

Audit data is intentionally restricted to actor, UTC timestamp, direct peer IP, trace ID, action and target identifiers. Use stable action names such as `member.ban`, `category.create`, `post.approve`. A creation callback can supply the resulting target ID. Do not pass secrets, user-provided content or JSON request bodies in these fields.

## Database protection and rollout

Migration: `20261007073718_AddAdminAuditFoundation` adds `public.admin_audit_logs`, FK to users without cascade, query indexes, RLS and revoked PUBLIC/anon/authenticated grants. PostgreSQL triggers reject UPDATE, DELETE and TRUNCATE, including ordinary direct SQL mutations by the backend connection. There is no bypass added for EF changes.

The backend database connection must have permission to SELECT/INSERT this table and use its identity sequence. The existing owner connection satisfies that. Do not expose audit through the Supabase Data API or grant browser roles access. A privileged database/schema owner can drop triggers or the table; these safeguards are not an external immutable archive. Rolling back the migration drops the audit table and its records.

The migration is not automatically applied at application startup. After configuring the private connection and JWT settings in this worktree, the designated database maintainer can apply it with:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Restart the backend after code changes. Log in as an existing Admin, authorize Swagger, then call the audit endpoints. Account provisioning must be handled by the project owner; there is no public self-promotion API.

The repository retains historical SQL Server migrations excluded from compilation. EF tooling can select the old snapshot file by basename when generating migrations; always inspect that the active snapshot is updated in `PostgresMigrations/` and the excluded snapshot remains unchanged. Run the existing model/snapshot parity test rather than suppressing pending-model warnings.

## Verification

Tests use the existing Testcontainers PostgreSQL fixture, or explicitly isolated loopback databases via `VEGANHELPER_MEMBER2_TEST_POSTGRES` / `VEGANHELPER_TEST_POSTGRES`. The fixtures refuse shared Supabase connections. The API fixture creates `anon`/`authenticated` roles and representative default table/sequence grants before migrations, so permission-revocation checks cannot pass merely because the roles were absent. The test database owner must have permission to create test roles when absent.

When provisioning a Windows local test database, use a Unicode-aware locale such as ICU `en-US`, matching the previously verified test environment. A database with the plain `C` locale does not lowercase Vietnamese names as the existing allergy queries expect; this affects the pre-existing concurrent custom-allergy test, independent of Admin functionality.

`AdminFoundationTests` covers authorization, audit DTOs/filter validation, transaction rollback and append-only database protection. The existing `NotificationModelTests` checks whole-model migration parity.
