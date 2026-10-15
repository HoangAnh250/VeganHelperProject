# Unified posts

User decision: remove recipe/article/video/community classification; retain cooking fields as optional. Scope: BE developer and FE dev. No commit, push, or shared database mutation.

1. Add HTTP and multipart regression cases for posts without classification, with and without cooking details. Observe existing failures.
2. Remove PostType from DTOs, entities, projections, search predicates and seed code. Preserve authorization, moderation, media and categories.
3. Generate PostgreSQL migration dropping CK_posts_1 and post_type; keep historical migrations. Verify upgrade preserves content and related data in isolated PostgreSQL.
4. Update FE requests and wording; leave optional cooking inputs empty, preserve zero, and hide absent recipe sections.
5. Run BE integration/unit tests, FE tests/build, browser checks and independent review.

Deployment: stop the old BE before dropping its required column, apply the reviewed migration, deploy the matching BE, then FE. This is a breaking response/schema change. Down restores a generic classification only, not original labels. Shared Supabase migration requires authorization after tests.

Validation: 241 BE unit and 305 PostgreSQL integration cases passed. FE focused and full tests plus Edge browser flow passed. Independent review found no issues. Shared Supabase/Azure unchanged.
