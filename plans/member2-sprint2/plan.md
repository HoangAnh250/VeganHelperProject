# Member 2 — Sprint 2 APIs

Status: completed; shared Supabase migration applied and verified 2026-10-05
Scope: FN22–FN25, FN27–FN29. FN26 and Member 1 features are deferred.
Base: cea0c5c; branch: feature/sprint2 (renamed at user's request).

1. Complete health APIs through repositories, shared AutoMapper profiles, validation, nullable-data handling, atomic allergies, six-month BMI history, ingredient picker and personal recipe allergy warnings.
2. Complete authenticated shop nearby/search/detail APIs with bounded pagination, database-side Haversine distance, publication filters, literal case-insensitive keyword matching, nullable real metadata, structured opening hours, media and menu keyword support.
3. Generate a separate PostgreSQL migration; preserve existing rows and existing migration files.
4. Add BLL boundary/algorithm tests plus real PostgreSQL repository and HTTP authorization/contract tests. Testcontainers use PostgreSQL, the current production engine.
5. Update API contracts and implementation/run notes. Leave changes reviewable locally; publishing is a separate request.

Compatibility: retain existing PageIndex pagination fields. History retains `history` while also exposing PagedResult fields. New shop metadata is nullable; unknown opening hours/rating are never invented. No external Places/AI credentials or jobs are needed for reading the shared shop catalogue.

Completed:
- FN22–FN25 and FN27–FN29 controllers/services/repositories, shared AutoMapper MappingProfile, validation and pagination (PageNumber alias added).
- PostgreSQL migration 20261002092558_AddMember2HealthAndShops, including RLS/revoked Data API privileges for three new shop tables and their identity sequences. Existing migration contents unchanged.
- Updated docs/Sprint2_API_Contracts.json and docs/Member2_Sprint2_Implementation.md; added a secret-free API configuration template and optional typed AutoMapper license configuration.

Validation (2026-10-02):
- API build succeeded: 0 warnings, 0 errors.
- Unit tests: 63 passed, 0 failed.
- Integration tests: 48 passed, 0 failed, 0 skipped; real PostgreSQL 17.11, new UTF-8/ICU test database on loopback port 55439. Simulated anon/authenticated default privileges verified revoked by the new migration.
- EF pending-model check: no model changes after migration.
- git diff --check: passed. Legacy SQL Server snapshot hash matches HEAD.
- Docker engine was unavailable; used the official portable PostgreSQL binaries in ignored .local-data instead. Test database was isolated from Supabase, and only the task-owned PostgreSQL server is stopped after verification.

Delivery: source remains uncommitted on feature/sprint2. No FE edits or GitHub publication. Real shop metadata still needs to be populated by the team; missing values remain null.

Shared database verification (2026-10-05, explicit user approval):
- Original error: PostgreSQL 42P01, shop_menu_items absent; migration was pending.
- Confirmed only AddMember2HealthAndShops pending and no conflicting new tables/columns before apply.
- Applied exactly that migration with EF's migration transaction; 0 pending afterward.
- Before/after counts unchanged: users 12, user_profiles 12, user_allergies 0, bmi_history 0, categories 15, shops 100, posts 26, post_ingredients 3.
- Verified actual shared-database Search/Detail/Nearby queries and RLS/denied anon/authenticated table privileges for all 3 new tables.
- Actual Swagger request search?Keyword=chay&PageIndex=1&PageSize=10 returned HTTP 200, totalCount 85, 10 items. Refreshed the existing Swagger login because its old access token had expired; no application restart required.
- Local screenshot evidence: .local-data/member2-maintenance/search-http-200.jpg, response region only (no tokens).
