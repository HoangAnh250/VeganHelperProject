# Admin dashboard statistics

All endpoints require a valid JWT and the active Admin policy. Reads are audited.
No schema migration is required.

## Performance and freshness

- Overview uses one parameterized PostgreSQL statement for all nine counts. Counts
  share the same statement snapshot, and pending content is still deduplicated.
- Top categories and their denominator are returned in one database query.
- Each statistics endpoint has an in-process cache with 15-second absolute expiry
  and a dedicated 128-entry size limit. Keys include resolved dates, interval, and
  limit as appropriate. Failed or cancelled loads are not cached.
- Authorization and audit execute on every request, including a cache hit. No
  permission result, token status, mutation, or moderation decision is cached.
- Statistical counts, including the pending badge, may lag changes by up to 15
  seconds. Content Management decision APIs continue to use current database state.
  Each API instance owns its own cache; restart clears it.
- FE loads each block independently. A slow/failed audit request no longer blocks
  statistics or charts, and changing grouping only refetches the activity endpoint.
  Date changes and Retry still refetch the relevant data. Obsolete requests are
  cancelled, and late responses cannot overwrite the selected range.

### Repository benchmark, 18 October 2026

Measured from the local BE machine against the configured Supabase database using
the same 30-day range, with an already-open connection and a read-only transaction.
Each range below covers three warm repetitions, excluding the first call. These
are repository timings, not end-to-end HTTP timings or page-load guarantees.

| Query | Before | After | SQL round trips before → after |
| --- | --- | --- | --- |
| Overview | 903–946 ms | 110–292 ms | 9 → 1 |
| Activity | 98–104 ms | 100–110 ms | 1 → 1 |
| Top categories | 200–202 ms | 102–103 ms | 2 → 1 |

The benchmark bypasses the statistics cache to measure query improvements alone.
Network conditions vary; initial connection, JIT, authorization and audit work add
latency to real HTTP requests. No index/schema or shared business data was changed.

| Endpoint | Additional parameters | Response |
| --- | --- | --- |
| `GET /api/admin/dashboard/overview` | None | `members`, `posts`, `moderation` |
| `GET /api/admin/dashboard/post-activity` | `interval=day\|week\|month` (default `day`) | `interval`, `items: [{period, postCount}]` |
| `GET /api/admin/dashboard/top-categories` | `limit=1..10` (default `3`) | `totalCategorizedPosts`, `items: [{categoryId, name, postCount, percentage}]` |

Each endpoint accepts optional `from` and `to` dates (`yyyy-MM-dd`), inclusive in UTC,
and returns the resolved dates. The default range is the last 30 days through today.
Ranges must be ordered, no longer than 366 days, and between year 2000 and today.
Invalid dates, intervals or limits return HTTP 400. Anonymous requests return 401;
non-admin requests return 403.

## Metric definitions

- `members.total`: current non-deleted accounts with the member role, including
  unverified, inactive and banned members. Administrators are excluded.
- `members.newInPeriod` / `newInPreviousPeriod`: member registrations during the
  selected range and preceding range of equal length. Growth is percentage change
  between these counts. `growthPercentage` is null if the preceding count is zero.
- `posts.totalPublished`: current non-deleted published posts without a moderation
  hold; it is an all-time current total, not restricted by the date filter.
- `posts.createdInPeriod` and activity: non-deleted posts created during the range,
  regardless of current moderation status. The chart measures creation, not approval.
- Activity includes zero-count buckets. Weeks begin on Monday; months begin on day
  one. Boundary buckets include only dates within the selected range, even when the
  bucket label precedes `from`.
- Top categories: active categories attached to currently public posts created
  during the range. Deleted, held, and unpublished posts are excluded. Ties sort by
  category ID. Percentages use distinct categorized posts as denominator, not just
  the top three. A post can belong to multiple categories, so shares can sum above
  100%. The UI's stacked strip shows relative counts among the displayed categories.
- Moderation counts are current pending content, independent of the date filter.
  AI flags follow the existing queue's revision/hold rule. Multiple pending reports
  for a post/comment count once. `totalPending` counts unique posts across both AI
  and user queues plus unique reported comments; it can be less than the sum of
  the three queue counts. Deleted targets are excluded.
- Current status/deletion rules apply even to historical ranges: these endpoints
  are not historical snapshots of publication status or membership.

## Existing audit endpoint

The FE also calls `GET /api/admin/audit-logs?PageIndex=1&PageSize=5&From=...&To=...`
with UTC timestamps covering the selected days. It shows admin ID, action, target,
and local browser date/time. The existing DTO does not contain success/failure or
admin display name; the UI uses `Recorded` and `Admin #id` rather than inventing them.
