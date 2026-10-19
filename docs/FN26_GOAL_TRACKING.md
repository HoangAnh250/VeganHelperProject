# FN26: weight goals, tracking and revisions

## Contract

All endpoints require the existing authenticated member JWT and enforce plan ownership. Existing `POST /api/MealPlans/generate` with `{ "startDate": "2026-10-21" }` remains a maintenance menu. Existing plans without a routine remain readable.

New generation fields:

```json
{
  "startDate": "2026-10-21",
  "mode": "weight_goal",
  "targetWeightKg": 58,
  "targetDate": "2027-01-13",
  "eligibilityConfirmed": true,
  "timeZoneId": "Asia/Ho_Chi_Minh",
  "remindersEnabled": true,
  "breakfastTime": "07:30:00",
  "lunchTime": "12:00:00",
  "dinnerTime": "18:30:00"
}
```

| Endpoint | Purpose |
|---|---|
| `POST /api/MealPlans/goal-preview` | Same input as generation; returns `allowed`, reasons, estimated TDEE, daily adjustment, calorie target and safety policy version. Does not save a plan. |
| `PUT /api/MealPlans/{id}/tracking` | `{date, mealTime, status, note?, expectedVersion}`. Status: `unlogged`, `followed`, `changed`, `skipped`. Future slots rejected. |
| `PUT /api/MealPlans/{id}/reminders` | `{enabled, timeZoneId, breakfastTime, lunchTime, dinnerTime, expectedVersion}`. Future times only; cannot move a future meal into the past. |
| `POST /api/MealPlans/{id}/revision-preview` | `{expectedVersion, profileFingerprint:"", eligibilityConfirmed, targetWeightKg?, targetDate?, nextWeek:false}`. Returns fresh safety, effective date, version and profile fingerprint. |
| `POST /api/MealPlans/{id}/revisions` | Same revision fields with returned fingerprint/version. Rechecks safety under a source-plan row lock and creates a new plan only after explicit confirmation. |

`nextWeek:true` proposes the next seven days after the source week. It reuses the goal, performs fresh screening and links the new week. The source state becomes `continued`; tracking and reminders for its current slots remain available. A future-days revision changes the source state to `superseded`. Only one successor per source can commit. The UI offers adjustment/continuation from the latest active version.

`GET` detail/list now return `routine` settings and dated entries. Version conflicts return 409; missing/unowned plans return 404; invalid or unsafe input returns 400. A revision with a changed health-profile fingerprint returns 409 and requires a new preview. Generation snapshots and past dates/meal logs are preserved; no in-place overwrite of meal history.

## Safety policy `goal-safety-v1`

This is conservative application screening, not clinical certification, a dietitian-reviewed diet or a guarantee of reaching a target. Existing meal data does not establish complete micronutrient or macronutrient adequacy.

- Adult age 20–65; explicit confirmation of no pregnancy/breastfeeding or clinician-managed nutrition needs.
- Current BMI from 18.5 to below 40; target BMI from 18.5 to below 25.
- Target date 7–730 days after the calculation date.
- Estimated adjustment: `(target kg − current kg) × 7700 / remaining days`.
- Maximum absolute adjustment 500 kcal/day and 20% of estimated TDEE; maximum weight change 0.5 kg/week.
- Minimum daily intake: 1500 kcal for male energy inputs, 1200 kcal for female inputs.
- Apply these limits to generated daily totals as well as the nominal target. Never clamp a rejected goal or automatically intensify restriction to catch up.
- Creation uses the selected start date; revision uses the effective future date and the remaining timeline. Both use the same evaluator. A future-week estimate remains an estimate based on the current profile.
- A difference of at least 100 kcal is labelled significant; smaller changes can keep the current plan.

These exact thresholds are **product policy choices**, not universal clinical safety boundaries. The linear 7700 approximation is not a physiological forecast; individual responses differ. Relevant references:

- [CDC: gradual weight loss](https://www.cdc.gov/healthy-weight-growth/losing-weight/index.html).
- [NHS: healthy ways to gain weight](https://www.nhs.uk/live-well/healthy-weight/managing-your-weight/healthy-ways-to-gain-weight/).
- [NIDDK Body Weight Planner](https://www.niddk.nih.gov/bwp): individual energy estimates and excluded populations.

## Reviews and notification delivery

The Hangfire job `meal-routine-review` runs every minute when `MealPlanning:WorkerEnabled=true`, on its dedicated `meal-planning` queue. Older application instances consuming only `default` cannot claim this new job type. It processes due reminders within a 30-minute recovery window, then checks completed three-day windows anchored to the goal's original start date across linked weeks. At least six logged meals are needed; below 70% followed produces a health-profile recommendation. `unlogged` is excluded from the denominator.

Notification keys use root goal/date/slot or root goal/review-window, so retries and copied historical dates do not duplicate notifications. Publishing joins the transaction and writes to the existing notification/push outbox. Review messages use `meal_reminder` and link to `/weekly-menu`. Browser push additionally requires configured WebPush and user browser permission. A sleeping Azure Free App Service cannot guarantee on-time delivery; old reminders outside the recovery window are intentionally not replayed.

## Deployment and local testing

1. Review/apply migration `20261017063121_AddMealGoalTracking` to the intended database. It only creates `meal_plan_routines`, `meal_plan_entries`, constraints and indexes; it enables RLS and revokes PUBLIC/anon/authenticated table/sequence access. Existing business rows are not deleted.
2. Restart the BE after recompilation. Do not run the new endpoints against an unmigrated database.
3. Set `MealPlanning__WorkerEnabled=true` where reminders/reviews should run. It is opt-in so deploying before Hangfire preparation does not unexpectedly start a worker.
4. If Hangfire is not prepared, use the existing `--prepare-background-jobs` administrative command **only after approval for the target shared database**. Existing installations can reuse their prepared schema.
5. WebPush additionally needs the existing WebPush/VAPID configuration.

Payment is integrated with the PayOS module. Goal previews and revisions use the real entitlement check through IMealPlanAccessPolicy. Trial generation and revisions share the three-generation limit; saving the menu and tracking entries is atomic. Saved-plan reads retain the payment module's trial and grace-period restrictions.

Use the PostgreSQL Docker integration fixture; it rejects nonlocal shared database connections. Do not point tests at Supabase.

```powershell
dotnet test -c Release -v quiet
```

Implementation branches: feature/fn26-ui-improvements (FE) and feature/fn26-improvements (BE), merged locally into dev and developer. Image data import remains outside scope.

## Merge with payment migrations (2026-10-19)

Migration 20261019033959_MergeMealPlanTrackingAndPayments aligns the latest migration target with the combined tracking/payment model. Its Up and Down methods do not alter tables or existing data. It follows the existing tracking and payment migrations. Generating this file does not apply migrations to any shared database.

## Applied environment state (2026-10-17)

After explicit owner authorization, migration `20261017063121_AddMealGoalTracking` was applied to Supabase project `brjltkherhhqqkryhsuq`. A subsequent read-only check confirmed both tables have RLS enabled, browser roles have no table privileges, no migrations remain pending, and the Hangfire schema exists. `MealPlanning:WorkerEnabled=true` was saved in the local API user-secrets store. Restart the BE process to compile the changed source and start the new worker; this does not change Azure settings.
