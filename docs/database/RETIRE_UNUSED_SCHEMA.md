# Retire unused schema objects

Migration: `20261108082645_RemoveUnusedSchemaObjects`.

Shared database status: not applied. The user confirmed that older BE instances are still active and requested application only after the compatible BE is deployed.

## Objects retired

- Tables `post_embeddings` and `user_available_ingredients`.
- Column `post_media.cloud_public_id`.
- Column `post_moderation_decisions.scan_id`, its FK and index.
- The decision actor check no longer references the removed column. Admin decisions still require an Admin ID; AI actor records still require a null Admin ID.

Entity classes, DbSets and property mappings are removed. The PostgreSQL snapshot is updated; historical migrations are preserved. `flags.moderation_scan_id`, scans, flags, decisions, posts, meals, ingredients and Shop tables remain intact.

## Release order

1. Build and test the new BE against a separate PostgreSQL database.
2. Deploy/restart every active BE instance with this model before applying the shared migration. The new model works with the old schema because the retired columns are nullable and the retired tables have no runtime consumers. Old BE instances can fail after the column drops because EF still selects those columns.
3. Back up the schema and recheck both tables are empty and both columns contain only NULL. The migration locks these tables before checking to prevent concurrent writes between the check and the drops. Lock acquisition times out after five seconds rather than waiting indefinitely. It aborts if data is present.
4. Apply this migration through the normal EF migration mechanism using the intended PostgreSQL connection. Do not execute isolated DROP statements or use CASCADE.
5. Verify migration history, absence of the four objects, and test post/media editing and Admin moderation against the deployed API.

The migration is transactional. Its Down recreates empty tables/nullable columns, their relationships and browser-access restrictions; it does not recover data. If AI-actor decisions are introduced after this migration, rollback must first review them because the previous actor constraint requires a non-null scan ID.

Earlier ERD and dictionary documents describe the historical schema. The runtime EF model and PostgreSQL migration history are the current source of truth after deployment.

No commit, push or production deployment is implied by preparing this migration.
