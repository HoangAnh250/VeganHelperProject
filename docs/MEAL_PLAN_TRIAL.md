# Meal-plan trial API

## Rules

- Each authenticated user receives up to three successfully saved weekly meal plans without an active paid package.
- A failed validation or generation does not use a trial. The trial is consumed only when the plan and all 21 schedule slots are committed successfully.
- An active paid package allows unlimited generations and does not reduce the trial balance.
- Trial plans remain readable after all three trial generations are used, including after paid-package viewing grace ends. When paid access is active or in its seven-day read-only grace period, all of the user's plans remain readable.
- The allowance is per account, not per device or session. A database row lock serializes trial saves for the same user so concurrent requests cannot exceed three.
- No membership/role is added for trial access.

## API contract

`GET /api/meal-plan-payments/access` remains the access endpoint. Its response now includes:

```json
{
  "status": "none",
  "canGenerate": true,
  "canViewExistingPlans": false,
  "expiresAt": null,
  "graceEndsAt": null,
  "trialGenerationsRemaining": 3,
  "canViewTrialPlans": false
}
```

`canViewExistingPlans` means the paid entitlement allows viewing all plans. `canViewTrialPlans` means the user can still access their trial-generated plans; it does not grant access to plans generated under an expired paid package.

`POST /api/MealPlans/generate` keeps its existing request and success response. A free account may call it while trials remain. Once all three successful trial plans have been saved, another request returns HTTP `402` with `errorCode: "MEAL_PLAN_TRIAL_LIMIT_REACHED"`. `GET /api/MealPlans` and `GET /api/MealPlans/{id}` return only trial-generated plans when the user has no active or grace-period paid viewing access.

## Database migration

The PostgreSQL migration adds `meal_plans.is_trial_generation BOOLEAN NOT NULL DEFAULT FALSE` and an index for per-user trial counting/filtering. Existing meal plans default to `false`; new trial-generated plans are marked `true`. The quota is computed from these saved rows, so there is no separate counter table or trial role.

The migration is local code until applied to a database. The preceding PayOS migration enables RLS and revokes direct table and sequence privileges from Supabase browser roles before the trial migration runs.
