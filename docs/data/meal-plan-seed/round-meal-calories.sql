-- Run in a caller-managed transaction.
CREATE TEMP TABLE rounded_menu_snapshots ON COMMIT DROP AS
SELECT p.id, p.generation_snapshot AS original,
       p.generation_snapshot
       || CASE WHEN jsonb_typeof(p.generation_snapshot->'targetDailyCalories')='number'
               THEN jsonb_build_object('targetDailyCalories', round((p.generation_snapshot->>'targetDailyCalories')::numeric))
               ELSE '{}'::jsonb END
       || jsonb_build_object('days', updated.days) AS snapshot
FROM public.meal_plans p
CROSS JOIN LATERAL (
    SELECT coalesce(jsonb_agg(
        CASE WHEN jsonb_typeof(d.day->'meals')='array' THEN
            d.day || jsonb_build_object('meals', cooked.meals)
            || CASE WHEN cooked.all_calories AND cooked.meal_count>0
                    THEN jsonb_build_object('totalCalories', cooked.total)
                    WHEN jsonb_typeof(d.day->'totalCalories')='number'
                    THEN jsonb_build_object('totalCalories', round((d.day->>'totalCalories')::numeric))
                    ELSE '{}'::jsonb END
        ELSE d.day END ORDER BY d.idx), '[]'::jsonb) AS days
    FROM jsonb_array_elements(CASE WHEN jsonb_typeof(p.generation_snapshot->'days')='array'
                                  THEN p.generation_snapshot->'days' ELSE '[]'::jsonb END)
        WITH ORDINALITY d(day, idx)
    CROSS JOIN LATERAL (
        SELECT coalesce(jsonb_agg(
            m.meal
            || CASE WHEN jsonb_typeof(m.meal->'calories')='number'
                    THEN jsonb_build_object('calories', round((m.meal->>'calories')::numeric))
                    ELSE '{}'::jsonb END
            || CASE WHEN jsonb_typeof(m.meal->'portionMultiplier')='number'
                    THEN jsonb_build_object('portionPercent', round((m.meal->>'portionMultiplier')::numeric*100))
                    ELSE '{}'::jsonb END ORDER BY m.idx), '[]'::jsonb) AS meals,
            count(*) AS meal_count,
            bool_and(jsonb_typeof(m.meal->'calories') IS NOT DISTINCT FROM 'number') AS all_calories,
            sum(CASE WHEN jsonb_typeof(m.meal->'calories')='number' THEN round((m.meal->>'calories')::numeric) END) AS total
        FROM jsonb_array_elements(CASE WHEN jsonb_typeof(d.day->'meals')='array'
                                      THEN d.day->'meals' ELSE '[]'::jsonb END)
            WITH ORDINALITY m(meal, idx)
    ) cooked
) updated
WHERE jsonb_typeof(p.generation_snapshot->'days')='array';

WITH changed_plans AS (
    UPDATE public.meal_plans p SET generation_snapshot=s.snapshot
    FROM rounded_menu_snapshots s
    WHERE p.id=s.id AND p.generation_snapshot=s.original AND s.snapshot IS DISTINCT FROM s.original
    RETURNING p.id
), changed_meals AS (
    UPDATE public.meals m SET total_calories=round(m.total_calories)
    WHERE m.total_calories>=0.5 AND m.total_calories<>round(m.total_calories)
    RETURNING m.id
)
SELECT (SELECT count(*) FROM changed_plans) AS rounded_plans,
       (SELECT count(*) FROM changed_meals) AS rounded_catalog_meals;
