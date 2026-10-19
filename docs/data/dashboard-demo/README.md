# Dashboard demonstration data — v1

Original English demonstration content for the Admin Dashboard and Content
Management pages. This is synthetic application data, not real member activity,
an external dataset, or expert-validated nutrition information.

## Batch contents

- 24 member accounts and profiles, visibly named `(Demo)`.
- 42 public posts: 30 recipes and 12 practical cooking/community articles.
- 120 structured cooking steps. Recipe ingredient lists also appear in post content.
- 3 held posts demonstrating a mismatch between vegan labeling and ingredients.
- 12 normal comments and 2 reported comment fixtures.
- 3 explicitly labeled AI flag fixtures, 3 user post reports covering 2 posts, and
  2 comment reports. The AI fixtures are **not** actual provider responses.
- Reuses existing active post categories; creates `Western Vegetarian Foods` if
  absent, so egg-and-dairy dishes do not appear under a vegan-only category.

Registration and publication dates are synthetic dates distributed across the six
months preceding the first import. The latest 30-day range includes 8 new demo
members and 29 demo posts (26 public plus 3 held). Previous 30-day registration
count is 4. Each account predates its authored posts.

All demo accounts use the `vh_dashboard_demo_` username prefix and
`@veganhelper.invalid` emails. Accounts are inactive, unverified, have no password,
and receive no external identities. They cannot sign in. Post content ends with
the stable `dashboard-demo-v1` demonstration marker. Comments and reports carry
explicit English demo labels.

No views/likes are invented. No real users, posts or audit records are updated or
deleted. Existing audit logs remain the actual management history. No health,
meal-planning, payment, or storage objects are changed.

## Import and verification

Run from the BE repository root with its normal `supabase.local.json` or
`ConnectionStrings__DefaultConnection` configuration:

```powershell
dotnet run --project tools/VeganHelper.DashboardDemo -- --preview
dotnet run --project tools/VeganHelper.DashboardDemo -- --apply
dotnet run --project tools/VeganHelper.DashboardDemo -- --verify
```

Preview is offline. Apply is an explicit write operation. It acquires a batch
advisory lock and imports in one transaction. Reapplying the complete batch adds
no duplicates and does not reset any later moderation decision. A partial/conflicting
batch is rejected rather than overwritten.

Verification checks the batch's row counts, disabled login state, absence of
queued AI scans and absence of notifications for demo users. The import inserts
no moderation scans or notification jobs. It therefore does not spend Gemini
quota or send browser/email notifications. AI fixture model name is
`demo-fixture-not-a-provider-response`.

The `--prepare-local-test` option only permits migrations on
`127.0.0.1/dashboard_demo_test`; it refuses shared databases. The full batch was
tested on an isolated PostgreSQL 17 Docker database, reapplied, and verified before
shared-database import. A failed test import rolled back completely.

Dashboard statistics may take up to 15 seconds to reflect an import due to their
short cache. Choose Weekly or Monthly grouping, or widen the date filter, to see
the synthetic history. The tool preserves each existing row and does not rewrite
historical dates on subsequent runs.
