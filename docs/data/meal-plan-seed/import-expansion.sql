SELECT pg_advisory_xact_lock(39120261012);
CREATE TEMP TABLE expansion_payload ON COMMIT DROP AS SELECT @catalog::jsonb AS data;
CREATE TEMP TABLE expansion_meals ON COMMIT DROP AS
SELECT dish->>'name' AS name, dish->>'diet' AS diet, (dish->>'kcal')::int AS kcal,
       dish, data->>'batch' AS batch,
       (SELECT image_url FROM public.meals
        WHERE owner_user_id IS NULL AND combo_name=dish->>'photo' AND nullif(trim(image_url),'') IS NOT NULL
        ORDER BY id LIMIT 1) AS image_url
FROM expansion_payload CROSS JOIN LATERAL jsonb_array_elements(data->'meals') dish;

CREATE TEMP TABLE expansion_ingredients ON COMMIT DROP AS
SELECT ingredient.key, i.id, i.calories_per_100g AS kcal
FROM expansion_payload CROSS JOIN LATERAL jsonb_each_text(data->'ingredients') ingredient
JOIN public.ingredients i ON i.name=ingredient.value AND i.default_unit='g';

DO $$ BEGIN
 IF (SELECT count(*) FROM expansion_meals)<>300 OR (SELECT count(DISTINCT lower(name)) FROM expansion_meals)<>300 THEN
   RAISE EXCEPTION 'Expected 300 distinct dishes';
 END IF;
 IF EXISTS(SELECT 1 FROM expansion_meals WHERE image_url IS NULL) THEN
   RAISE EXCEPTION 'A reference meal image is missing in Supabase';
 END IF;
 IF EXISTS (SELECT 1 FROM expansion_payload CROSS JOIN LATERAL jsonb_each_text(data->'ingredients') k
   WHERE (SELECT count(*) FROM expansion_ingredients i WHERE i.key=k.key AND i.kcal IS NOT NULL)<>1) THEN
   RAISE EXCEPTION 'Missing or ambiguous ingredient in Supabase';
 END IF;
 IF EXISTS (SELECT 1 FROM expansion_meals m WHERE m.kcal IS DISTINCT FROM
   (SELECT round(sum(v.value::numeric*i.kcal/100)) FROM jsonb_each_text(m.dish->'ingredients') v
    JOIN expansion_ingredients i ON i.key=v.key)) THEN
   RAISE EXCEPTION 'Energy does not match live ingredient values';
 END IF;
 IF EXISTS (SELECT 1 FROM expansion_meals s JOIN public.meals m ON lower(m.combo_name)=lower(s.name)
   WHERE m.owner_user_id IS NULL AND (m.description IS NULL OR position(s.batch IN m.description)=0)) THEN
   RAISE EXCEPTION 'A dish name already belongs to another batch';
 END IF;
END $$;

INSERT INTO public.meals(owner_user_id,combo_name,description,servings,total_calories,diet_type,image_url)
SELECT NULL,s.name,
       'One serving. Estimated calories from listed ingredient weights. Representative photo. Batch: '||s.batch,
       1,s.kcal,s.diet,s.image_url
FROM expansion_meals s WHERE NOT EXISTS (
  SELECT 1 FROM public.meals m WHERE m.owner_user_id IS NULL AND lower(m.combo_name)=lower(s.name));
CREATE TEMP TABLE expansion_ids ON COMMIT DROP AS
SELECT s.*,m.id FROM expansion_meals s JOIN public.meals m ON m.owner_user_id IS NULL AND m.combo_name=s.name;
INSERT INTO public.meal_ingredients(meal_id,ingredient_id,calculated_quantity,unit)
SELECT m.id,i.id,v.value::numeric,'g' FROM expansion_ids m
CROSS JOIN LATERAL jsonb_each_text(m.dish->'ingredients') v JOIN expansion_ingredients i ON i.key=v.key
ON CONFLICT(meal_id,ingredient_id) DO NOTHING;
INSERT INTO public.meal_suitable_times(meal_id,meal_time)
SELECT m.id,t.value FROM expansion_ids m CROSS JOIN LATERAL jsonb_array_elements_text(m.dish->'slots') t
ON CONFLICT(meal_id,meal_time) DO NOTHING;
INSERT INTO public.meal_cooking_steps(meal_id,step_number,instruction)
SELECT m.id,s.ordinality::smallint,s.value FROM expansion_ids m
CROSS JOIN LATERAL jsonb_array_elements_text(m.dish->'steps') WITH ORDINALITY s
ON CONFLICT(meal_id,step_number) DO NOTHING;

DO $$ BEGIN
 IF (SELECT count(*) FROM expansion_ids)<>300 OR EXISTS (
   SELECT 1 FROM expansion_ids x JOIN public.meals m ON m.id=x.id
   WHERE m.total_calories IS DISTINCT FROM x.kcal OR m.servings<>1 OR m.diet_type<>x.diet
      OR nullif(trim(m.image_url),'') IS NULL
      OR (SELECT count(*) FROM public.meal_ingredients i WHERE i.meal_id=m.id)<>(SELECT count(*) FROM jsonb_each(x.dish->'ingredients'))
      OR (SELECT count(*) FROM public.meal_suitable_times t WHERE t.meal_id=m.id)<>jsonb_array_length(x.dish->'slots')
      OR (SELECT count(*) FROM public.meal_cooking_steps c WHERE c.meal_id=m.id)<>jsonb_array_length(x.dish->'steps')
 ) THEN RAISE EXCEPTION 'Imported dish or child record count mismatch'; END IF;
END $$;

SELECT count(*) AS dishes,
 (SELECT count(*) FROM public.meal_ingredients i JOIN expansion_ids x ON x.id=i.meal_id) AS ingredient_links,
 (SELECT count(*) FROM public.meal_suitable_times t JOIN expansion_ids x ON x.id=t.meal_id) AS slot_links,
 (SELECT count(*) FROM public.meal_cooking_steps c JOIN expansion_ids x ON x.id=c.meal_id) AS cooking_steps
FROM expansion_ids;
