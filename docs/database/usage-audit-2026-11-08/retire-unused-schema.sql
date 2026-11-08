START TRANSACTION;
SET LOCAL lock_timeout = '5s';
LOCK TABLE public.post_embeddings, public.user_available_ingredients,
    public.post_media, public.post_moderation_decisions IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM public.post_embeddings)
       OR EXISTS (SELECT 1 FROM public.user_available_ingredients)
       OR EXISTS (SELECT 1 FROM public.post_media WHERE cloud_public_id IS NOT NULL)
       OR EXISTS (SELECT 1 FROM public.post_moderation_decisions WHERE scan_id IS NOT NULL) THEN
        RAISE EXCEPTION 'Retired schema objects contain data. Archive and review before removing them.';
    END IF;
END $$;

ALTER TABLE post_moderation_decisions DROP CONSTRAINT "FK_post_moderation_decisions_post_moderation_scans_scan_id";

DROP TABLE post_embeddings;

DROP TABLE user_available_ingredients;

DROP INDEX "IX_post_moderation_decisions_scan_id";

ALTER TABLE post_moderation_decisions DROP CONSTRAINT "CK_moderation_decision_actor";

ALTER TABLE post_moderation_decisions DROP COLUMN scan_id;

ALTER TABLE post_media DROP COLUMN cloud_public_id;

ALTER TABLE post_moderation_decisions ADD CONSTRAINT "CK_moderation_decision_actor" CHECK ((actor_type = 'admin' AND admin_id IS NOT NULL) OR (actor_type = 'ai' AND admin_id IS NULL));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108082645_RemoveUnusedSchemaObjects', '10.0.12');

COMMIT;
