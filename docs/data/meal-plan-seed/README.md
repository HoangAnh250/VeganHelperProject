# Meal planning sample data

Batch: `VH-MEAL-SEED-20261012-v1`. Imported into the shared Supabase project `brjltkherhhqqkryhsuq` on 12 October 2026.

This is generated sample data, **not dietitian-reviewed recipes or personalized nutrition advice**. Recipe names, descriptions, ingredients and template names are in English. All weekly templates have one dish per breakfast, lunch and dinner slot. No frontend or application code was changed.

## Imported records

| Table | Added | Notes |
|---|---:|---|
| ingredients | 90 | 11 matching existing ingredients reused; 111 total ingredients after import |
| meals | 200 | 140 vegan, 60 lacto-ovo vegetarian; 60 Vietnamese/Vietnamese-inspired dishes and 140 international dishes |
| meal_ingredients | 1,144 | All quantities in grams; no missing ingredient references |
| meal_plans | 30 | 20 vegan and 10 lacto-ovo vegetarian shared templates |
| meal_plan_schedule | 630 | Exactly 7 days × 3 meals for each template |

The 21 pre-existing ingredient records were not overwritten or deleted. The batch uses the existing `combo_name` column to store a **single dish name**; renaming the column is a separate application/schema change. Each dish has `servings=1`, so its total calories correspond to the listed ingredient quantities for one serving. Scheduled `portion_multiplier=1.00` uses that serving unchanged.

Templates have no owner, dates, height, weight or BMI. They use `is_template=true`, `generation_source=template`, `status=completed` and empty allergy/pantry snapshots. Empty allergy snapshots do **not** mean allergen-free food. These templates do not implement FN26 generation or meal-reminder scheduling. Every lacto-ovo template contains egg-based dishes; vegan templates contain no egg or dairy ingredient entries.

## Nutrition provenance and limitations

`ingredients.csv` contains each selected USDA FDC ID, original food description, energy value, preparation state and source URL. Source: USDA SR Legacy April 2018, downloaded from https://fdc.nal.usda.gov/download-datasets/. USDA publishes FoodData Central data under CC0: https://fdc.nal.usda.gov/api-guide/. Recipe combinations were authored for this project, not copied from an expert-approved cookbook or meal plan.

Calories are estimated as the sum of edible ingredient grams × USDA kcal/100 g, rounded to two decimals. Raw and cooked ingredients are named explicitly. This estimate does not model cooking yield, oil loss, nutrient retention or commercial brand variation. Water and salt have zero energy; missing nutrient values were not replaced by zero. Generic food references are used for vegan bread, egg-free pasta/tortillas and vegetarian-rennet cheese; the actual purchased products must satisfy those ingredient requirements. USDA reference records alone do not certify vegan status or vegetarian rennet.

These are varied sample menus, not a promise of complete nutrition. No calorie goal or individualized adjustment was requested. The template calorie ranges are in `meal_plans.csv`; some days may not meet a particular user's energy requirements. No named professional has approved these templates. Both dish descriptions and template titles disclose their unreviewed status.

## Files and validation

- `ingredients.csv`: 101 ingredient references, including the 11 reused records.
- `meals.csv`: 200 unique names, ingredient sets and quantity signatures, with cuisine and suitable meal-time metadata.
- `meal_ingredients.csv`: full recipe quantities linked through stable local keys.
- `meal_plans.csv` and `meal_plan_schedule.csv`: 30 distinct complete weekly schedules.
- `seed-data.json`: canonical seed payload.
- `validation-summary.json`: counts, cuisine distribution and energy ranges.
- `import.sql`: transaction-based, add-only import; guards conflicting existing values instead of overwriting them.
- `dry-run.sql`: same import ending in ROLLBACK.
- `verify.sql`: read-only queries to inspect the imported data.

Before import, rollback previews and an independent technical review checked names, recipe references, energy arithmetic, diet types and complete weekly slots. A repeat preview after import inserted zero rows, proving the batch does not duplicate its own data. The SQL uses temporary tables only; no permanent tables, columns, permissions or application migrations were added. The data files contain no database credentials. No commit or push was performed.

## Subsequent meal-time schema addition — 12 October 2026

After the original sample import, migration `20261012143219_AddMealSuitableTimes` added `meal_suitable_times` in the shared database and the EF Core model. This is a separate change from the data-only import described above. The table stores allowed meal times for each dish, with composite primary key `(meal_id, meal_time)`, a foreign key to `meals`, a check allowing only `breakfast`, `lunch`, `dinner`, and an index starting with `meal_time`. Deleting a meal removes its suitability entries; existing meal schedule constraints still apply.

RLS is enabled and browser-facing PUBLIC/anon/authenticated table privileges are revoked, matching the existing trusted .NET API boundary. No Data API policies were added.

`import-suitable-times.sql` populated the 200 dishes in batch `VH-MEAL-SEED-20261012-v1` using their CSV `slots` metadata: 55 breakfast entries and 145 entries each for lunch and dinner (345 links). It matches exact dish names plus the batch marker, does not infer times from existing schedules, and inserts missing links only. Run the EF migration before this seed script for a separate database; do not rerun the non-idempotent migration SQL manually on this already migrated shared database.

`verify-suitable-times.sql` verifies seed coverage, counts, invalid values, duplicate keys, missing meal references and RLS/grants inside a rolled-back transaction. Both migration preview and post-import checks passed on PostgreSQL. No frontend changes, commit or push were made for this schema addition. FN26 generation itself is not implemented by this table.
