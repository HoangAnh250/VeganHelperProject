\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
DO $$
DECLARE sample_id bigint;
BEGIN
    SELECT id INTO STRICT sample_id FROM meals WHERE description LIKE '%VH-MEAL-SEED-20261012-v1%' ORDER BY id LIMIT 1;
    IF (SELECT count(*) FROM meal_suitable_times t JOIN meals m ON m.id=t.meal_id WHERE m.description LIKE '%VH-MEAL-SEED-20261012-v1%') <> 345 THEN
        RAISE EXCEPTION 'Expected 345 seed suitability links';
    END IF;
    IF EXISTS (SELECT 1 FROM meals m WHERE m.description LIKE '%VH-MEAL-SEED-20261012-v1%' AND NOT EXISTS (SELECT 1 FROM meal_suitable_times t WHERE t.meal_id=m.id)) THEN
        RAISE EXCEPTION 'A seed dish is missing suitable meal times';
    END IF;
    BEGIN
        INSERT INTO meal_suitable_times VALUES (sample_id, 'snack');
        RAISE EXCEPTION 'Invalid meal time accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO meal_suitable_times SELECT meal_id, meal_time FROM meal_suitable_times LIMIT 1;
        RAISE EXCEPTION 'Duplicate accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO meal_suitable_times VALUES (-1, 'breakfast');
        RAISE EXCEPTION 'Missing meal reference accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    IF NOT (SELECT relrowsecurity FROM pg_class WHERE oid='public.meal_suitable_times'::regclass) THEN
        RAISE EXCEPTION 'RLS is disabled';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.role_table_grants WHERE table_schema='public' AND table_name='meal_suitable_times' AND grantee IN ('PUBLIC','anon','authenticated')) THEN
        RAISE EXCEPTION 'Unexpected Data API grants';
    END IF;
END $$;
SELECT meal_time, count(*) FROM meal_suitable_times GROUP BY meal_time ORDER BY meal_time;
ROLLBACK;
