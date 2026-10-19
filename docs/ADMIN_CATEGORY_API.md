# Admin category slug behavior

`POST /api/admin/categories` and `PUT /api/admin/categories/{id}` still require
Admin authorization. Their `slug` field is now optional; no database migration is
required.

```json
{
  "name": "Roasted Vegetable Soups",
  "postCategoryKind": "recipe"
}
```

On creation, omitting or blanking `slug` generates `roasted-vegetable-soups` from
the trimmed name. Generation lowercases, removes accents (including mapping `đ`
to `d`), and replaces punctuation/spacing with single hyphens. Names with no ASCII
letters/digits use the base `category`. A collision with another category's slug
adds a numeric suffix starting at `-2`. Allocation uses the existing PostgreSQL
category write lock, so simultaneous creates cannot allocate the same slug.

Duplicate category names still return 409. A provided slug retains the previous
validation and conflict behavior, preserving compatibility with existing API clients.
The category name remains limited to 100 characters and a provided slug to 120.

On update, omitted or blank `slug` preserves the existing slug even when the name
changes. This keeps links stable. Existing API clients can still explicitly change
a slug by submitting a valid unused value. The FE form only asks for name and type.

Response DTOs continue to return the persisted `slug` for list/detail displays.
