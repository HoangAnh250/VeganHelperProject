# Supabase migration

Status: complete (2026-10-01)

Direction: move Vegan Helper's existing SQL Server data to shared Supabase PostgreSQL. Preserve every mapped table, IDs, relationships and password hashes; preserve .NET API/DTO contracts and React UI. Supabase Auth/Storage are outside this migration.

1. Replace SQL Server provider/mappings with PostgreSQL, archive SQL Server migrations without compiling them, generate a PostgreSQL baseline. Verify schema creation and constraints on PostgreSQL.
2. Repair case-sensitive category/auth lookups and deterministic feed/MyPosts pagination, preserving projections. Verify repository behavior against PostgreSQL and run existing BLL tests.
3. Make migrations and seeds explicit commands, port shop seed, make post seed repeatable. Verify repeated seeding doesn't overwrite existing data.
4. Build a read-only SQL Server export and transactional PostgreSQL import tool. Export all mapped tables; import only into an empty application database, preserve IDs, validate row counts/content and fix identity sequences. SQL Server remains untouched. Verify round trip and rollback on PostgreSQL.
5. Provide ignored local config and Supabase setup/run instructions. Apply to the user's new Supabase target once configured and identified; verify counts and real API endpoints.

Dependencies: user is creating a Supabase project and must enter its connection details locally. Do not claim cloud migration completed until the real target has passed import and API checks.

Completed: Supabase project brjltkherhhqqkryhsuq imported all 193 original rows across 32 application tables. Exact field/count verification passed before adding seed. The 20 existing demo recipes were then seeded, and verify-preserved confirmed every original row/field remained unchanged with 122 expected additional rows. Build succeeded; 14 unit tests and 9 PostgreSQL integration tests passed. Real API database status, categories, feed (including filters), health and Swagger returned HTTP 200. A second import correctly refused the nonempty target. SQL Server was not changed. All changes remain uncommitted and unpushed at the user's request.

Risks/checks: timestamp kind conversion must treat SQL Server DATETIME2 event timestamps as UTC; preserve date-only values. Foreign-key ordering and comment self references require deterministic import ordering. Fail on missing source columns, extra source tables, nonempty target, mismatched data or invalid values. Credentials and exports stay ignored. Supabase Data API must not expose backend-managed application tables.
