# Database usage audit — 2026-11-08

## Scope and evidence

- Graphify 0.9.104 structural scan: 374 BE source files, 3,728 nodes, 11,091 edges. Local output: `src/graphify-out/graph.json`. No remote AI used.
- FE Graphify scan: 222 files, 821 nodes, 3,321 edges. Generated graph archived in `%TEMP%/vh-schema-audit/fe-graphify-out` to keep generated files outside FE source control. CSS and some test-only modules are not represented by this symbol extractor; text searches and tests supplement the graph.
- EF PostgreSQL snapshot inventory: 53 business tables, 424 distinct columns. Repeated identity-builder references are excluded from the property count.
- Token-reference index across 461 BE/FE runtime source files. `tables.csv` and `columns.csv` enumerate the entire snapshot. Reference counts are search evidence, not proof of usage or non-usage; names shared by multiple entities can produce false positives.
- Live Supabase public catalog: 54 tables, 426 columns including the two migration-history columns; no additional business table and no additional business column outside the EF snapshot.
- Inspected public constraints, policies, functions and triggers. Protected audit/moderation history and managed Supabase/Hangfire schemas must be retained.
- Queried only metadata and aggregate counts. No user content exported. No database object deleted or altered.

## Reviewed candidates

| Object | Evidence | Assessment |
| --- | --- | --- |
| `shop_media` | `ShopRepository.Query` includes `Shop.Media`; DTO mapping returns media | Keep: used indirectly through EF navigation. Initial token scan was a false positive. |
| `shop_menu_items` | Shop detail includes `MenuItems`; Shop search also filters menu item names | Keep: used indirectly through EF navigation. Initial token scan was a false positive. |
| `post_embeddings` | No direct runtime consumer found; 0 live rows | Reserved RAG schema, explicitly retained in `docs/database/CHANGES_VI.md`. Candidate for feature retirement, not an orphan table. Still mapped in EF. |
| `user_available_ingredients` | No direct runtime consumer found; 0 live rows | Reserved pantry schema, documented in the meal-planning ERD. Candidate for feature retirement; still mapped in EF. |
| `post_media.cloud_public_id` | No runtime use found; 0 non-null rows | Legacy Cloudinary identifier; strongest cleanup candidate. Remove EF mapping/property and deploy compatible BE before dropping this column. |
| `post_moderation_decisions.scan_id` | No direct runtime assignment found; 0 non-null rows | FK to scan history. Requires explicit retirement of this audit association, EF changes and a coordinated release. |
| `meal_plans.template_name` | 30 non-null live rows | Preserve data; do not delete based on absence of direct runtime property references. |
| `ingredients.calories_per_100g` | 107 non-null live rows | Preserve nutrition data; do not delete. |
| `posts.calories_per_serving` | 20 non-null live rows; written by PostSeeder | Preserve: seed path uses this column. |

## Why direct deletion is unsafe

Every candidate remains part of the EF model. EF can select mapped columns when materializing entities even if business code never reads that property explicitly. Dropping `cloud_public_id` or `scan_id` first can break the currently deployed API. An empty table can also still be a planned feature or carry relational dependencies.

A safe retirement requires: choose objects to retire; archive any data/schema; remove entity mappings and usages; add a reviewed EF migration with restrictive drops (no CASCADE); test against a separate PostgreSQL database; deploy the compatible BE to every active instance; then apply the migration to the shared database. Pending team changes and other deployed branches are not covered by this local source audit.

No drop migration was applied in this audit. The public schema currently matches the model; `__EFMigrationsHistory`, authentication, storage, realtime and Hangfire internals are not unused application tables.

## Validation

- BE unit tests: 396 passed.
- Initial full integration run on isolated Docker PostgreSQL: 365 passed, 1 failed (`NotificationApiTests.DisabledPush_ReportsStatusAndRefusesSubscriptions`). The fixture inherited enabled WebPush settings from local `.env`; it now explicitly disables WebPush using `PostConfigure` for isolated API tests. All 13 notification API tests passed after this correction. Final full integration rerun: 366/366 passed.
- FE suite: 72 test files, 515 tests passed.

This report is a source/catalog audit, not an end-to-end browser or production health certification.

## Follow-up: approved retirement

The user subsequently requested retirement of both reserved tables and both nullable legacy columns. Runtime entity/mapping cleanup and migration `20261108082645_RemoveUnusedSchemaObjects` are now prepared. See `../RETIRE_UNUSED_SCHEMA.md` and `retire-unused-schema.sql` for the release order and exact SQL. Shared Supabase application is explicitly deferred until older BE instances have been upgraded.

After cleanup, the complete BE solution build/test run passed 396 unit tests and 367 integration tests against isolated PostgreSQL. EF reports no pending model changes. FE code was unchanged by this retirement; its preceding full suite passed 515 tests. Migration guard locks are additionally verified in the focused schema test.
