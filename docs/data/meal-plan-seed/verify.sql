\set ON_ERROR_STOP on
BEGIN READ ONLY;
SELECT 'ingredients_total' AS entity,count(*) FROM public.ingredients
UNION ALL SELECT 'seed_meals',count(*) FROM public.meals WHERE description LIKE '% Batch: VH-MEAL-SEED-20261012-v1'
UNION ALL SELECT 'seed_meal_ingredients',count(*) FROM public.meal_ingredients mi JOIN public.meals m ON m.id=mi.meal_id WHERE m.description LIKE '% Batch: VH-MEAL-SEED-20261012-v1'
UNION ALL SELECT 'seed_templates',count(*) FROM public.meal_plans WHERE is_template AND template_name LIKE '% - Unreviewed Sample'
UNION ALL SELECT 'seed_slots',count(*) FROM public.meal_plan_schedule s JOIN public.meal_plans p ON p.id=s.meal_plan_id WHERE p.is_template AND p.template_name LIKE '% - Unreviewed Sample';
SELECT diet_type,count(*) FROM public.meals WHERE description LIKE '% Batch: VH-MEAL-SEED-20261012-v1' GROUP BY diet_type;
SELECT diet_type,count(*) FROM public.meal_plans WHERE is_template AND template_name LIKE '% - Unreviewed Sample' GROUP BY diet_type;
SELECT count(*) AS incomplete_templates FROM (
 SELECT p.id FROM public.meal_plans p LEFT JOIN public.meal_plan_schedule s ON s.meal_plan_id=p.id
 WHERE p.is_template AND p.template_name LIKE '% - Unreviewed Sample'
 GROUP BY p.id HAVING count(s.id)<>21 OR count(DISTINCT(s.day_of_week,s.meal_time))<>21
) bad;
SELECT count(*) AS mismatched_diets FROM public.meal_plan_schedule s
JOIN public.meal_plans p ON p.id=s.meal_plan_id JOIN public.meals m ON m.id=s.meal_id
WHERE p.is_template AND p.template_name LIKE '% - Unreviewed Sample' AND p.diet_type<>m.diet_type;
SELECT count(*) AS calorie_mismatches FROM (
 SELECT m.id,m.total_calories,round(sum(mi.calculated_quantity*i.calories_per_100g/100),2) calculated
 FROM public.meals m JOIN public.meal_ingredients mi ON mi.meal_id=m.id JOIN public.ingredients i ON i.id=mi.ingredient_id
 WHERE m.description LIKE '% Batch: VH-MEAL-SEED-20261012-v1' GROUP BY m.id,m.total_calories
) sums WHERE total_calories IS DISTINCT FROM calculated;
SELECT count(*) AS duplicate_dish_names FROM (
 SELECT lower(combo_name) FROM public.meals WHERE description LIKE '% Batch: VH-MEAL-SEED-20261012-v1'
 GROUP BY lower(combo_name) HAVING count(*)>1
) duplicates;
SELECT count(*) AS bad_recipe_units FROM public.meal_ingredients mi JOIN public.meals m ON m.id=mi.meal_id
WHERE m.description LIKE '% Batch: VH-MEAL-SEED-20261012-v1' AND (mi.unit<>'g' OR mi.calculated_quantity IS NULL OR mi.calculated_quantity<=0);
COMMIT;
