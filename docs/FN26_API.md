# FN26 — rule-based weekly meal generation

> This document describes the original maintenance contract. For weight goals, calendar tracking, reminders, safety checks and confirmed revisions, see [FN26_GOAL_TRACKING.md](FN26_GOAL_TRACKING.md). Existing maintenance requests remain supported.

Backend only, branch `feature/meal-plan`. No Gemini/API key, frontend changes or Git publication are required. The endpoint generates and persists a menu in one request. It supports adult maintenance-calorie estimates, not automated weight-loss/gain treatment or a guarantee of healthy BMI. Recipes and weekly examples remain unreviewed sample data.

## API contract

All endpoints require a valid existing JWT. User identity comes from the token; no user ID, calories, BMI, allergy list or diet override is accepted from the generation request.

| Method | Route | Result |
|---|---|---|
| POST | `/api/MealPlans/generate` | 201 with persisted plan ID, Location header and full generated snapshot |
| GET | `/api/MealPlans/{id}` | 200 for an owned non-template plan; 404 for absent/another user's plan |
| GET | `/api/MealPlans?pageIndex=1&pageSize=10` | Shared paginated result, newest first, owned plans only |

Example request (use today or a date within the next year; validation uses UTC):

```json
{ "startDate": "2026-10-19" }
```

The response has `id`, `startDate`, `endDate`, `status`, `dietType`, `createdAt`, and `menu`. `menu` contains `algorithmVersion`, `goal`, `targetDailyCalories`, `bmi`, `energyProfile`, `allergies`, `notes`, and seven `days`. Every day has `dayIndex` (1–7 relative to start), `date`, `dayOfWeek` (ISO Monday=1, Sunday=7), `totalCalories`, and three `meals`. Each meal has `mealId`, `name`, `mealTime`, `portionMultiplier`, `calories`, and scaled ingredient quantities/units.

## Generation rules

1. Read the profile, allergy list and accessible shared/owned dish catalog in one repeatable-read input transaction. Other users' private dishes are excluded.
2. Validate height 100–250 cm, weight 30–200 kg, known birth date and age 20–120, supported male/female energy inputs, activity level, and vegan/lacto-ovo diet. BMI is calculated from the actual recorded weight/height; generation never creates BMI-history entries or chooses a target BMI.
3. Reuse Mifflin–St Jeor resting-energy estimation multiplied by existing activity factors (1.2, 1.375, 1.55, 1.725, 1.9). This estimates maintenance calories; no automatic calorie deficit/surplus. The existing health endpoints retain their previous age rules; the generator requires age >=20.
4. Expand supported conservative allergy families (peanut, soy, dairy, egg, gluten, tree nuts, sesame, seafood, mustard, celery, lupin, sulfites), including selected Vietnamese aliases. Ingredient-ID filtering always applies. Unrecognized aliases need a matching ingredient actually used in an accessible recipe; unrelated custom ingredient rows and the IsCustom flag cannot establish resolution. If not resolved, return 409 instead of ignoring it.
5. Exclude dishes with unknown/nonpositive calories or servings, empty ingredients, missing/nonpositive quantities, incompatible diet, allergens, or unsuitable breakfast/lunch/dinner metadata.
6. Allocate 30%/35%/35% of target calories to the three meals. Try up to 40 randomized greedy builds. Rank candidates by calorie deviation, distance from the original portion, reuse of the largest-quantity ingredient, and a small random tie/variety factor. Dish IDs never repeat in a week. This is bounded heuristic search, not proof of globally optimal nutrition or feasibility.
7. Calculate base calories/quantities as recipe totals divided by `servings`; multiply by `portionMultiplier` rounded to 2 decimals, limited to 0.75–1.5. Calories are rounded to 2 decimals, ingredient quantities to 3. Only accept seven complete days within ±10% of the daily target. Ratios, portion bounds and tolerance are engineering choices, not medical thresholds.
8. Store the plan and all 21 schedule rows in one write transaction. A failed schedule insert rolls back the plan. `generation_source='manual'` fits the existing enum; the snapshot identifies `algorithmVersion='rules-v1'` and `goal='maintenance'`. Repeated POST calls create separate plans; this API has no idempotency key.
9. Store the full response menu as nullable JSONB `meal_plans.generation_snapshot`, including captured profile/date/target, allergies, names, calories, quantities and notes. Reads use this snapshot, so later catalog/profile edits do not alter historical menus. Legacy plans without a snapshot return `menu:null`; they are not regenerated implicitly.

If no complete solution is found, return 409 without storing a partial or failed plan. Missing/invalid energy inputs and invalid dates return 400, guests/expired JWT return 401. Existing middleware formats these errors. Never relax allergies, diet, uniqueness or serving limits to satisfy calories.

## Scope and limitations

The three daily dishes account for the menu's calories; additional food/drinks and actual consumption are outside the calculation. Calories alone cannot establish protein, micronutrient or overall nutritional adequacy. Allergen families conservatively over-exclude some ingredients; product composition, cross-contact and unknown aliases need human verification. Adult formulas do not cover pregnancy, breastfeeding or medical nutrition care. No cuisine scoring is claimed: cuisine metadata remains in seed files, not the dish schema. Variety comes from distinct dishes and the ingredient-reuse penalty.

The 30 existing templates are preserved; generation currently builds from eligible dishes rather than selecting/adapting a template. Available-pantry filtering, dish swaps, archive/delete and meal reminder scheduling are outside this endpoint. FN26 does not send notifications itself.

## Database and running

Migration `20261012145143_AddMealPlanGenerationSnapshot` adds one nullable JSONB column. It was previewed with ROLLBACK and then applied to the shared Supabase project `brjltkherhhqqkryhsuq`. Existing rows/templates are unchanged. The earlier `AddMealSuitableTimes` migration and 345 seeded suitability entries are prerequisites.

For a different database, apply normal EF migrations with the configured connection:

```powershell
dotnet ef database update --project src/VeganHelper.DAL --startup-project src/VeganHelper.API
```

Stop the running BE first, then restart from the checkout containing these changes:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

In Swagger, authorize, save a valid health profile and allergy list using existing HealthProfile APIs, POST generate, follow Location to GET, and list own plans. To check errors, use an unresolved custom allergy or missing profile. All test writes were to isolated localhost databases, not shared Supabase users.

## Verification

- 217 unit tests passed, including the actual 200-dish seed at several diets/energy profiles, portion-per-serving math, seven-day coverage, 21 unique dishes, allergy-family expansion, unknown aliases/reselection/unused custom rows, impossible targets, cancellation and health-service regression.
- 43 PostgreSQL/API integration tests passed on a fresh UTF-8/ICU local database: FN26 creation/read/list/ownership, calendar dates, immutable snapshots, rejection without writes, atomic rollback, and existing health/allergy APIs.
- Initial test issues: stale incremental EF snapshot was resolved by a full rebuild; locale C failed an existing Vietnamese-case allergy test, confirmed with `lower('ĐẬU MỚI')`, and the final fixture used UTF-8/ICU. These are retained as environment findings, not hidden by changes to production logic.
- Scoped independent review found two unresolved-alias bypasses; both were fixed and regression-tested, with no remaining actionable findings in the recheck. No commit/push or frontend changes.
