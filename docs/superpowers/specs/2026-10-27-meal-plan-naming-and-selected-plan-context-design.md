# Meal plan naming and selected-plan context

**Status:** Draft for user review
**Date:** 2026-10-27

## Goal

Make the “From your profile to your plate” section reflect the saved meal plan currently selected by the user, and let users name and later rename their saved meal plans so they can distinguish them.

## Current behavior

- `WeeklyMealPlanner` owns the selected `plan`, but passes only `plan.menu` to `MenuJourney`.
- `MenuJourney` derives some values from the saved menu snapshot, while it cannot use the full selected plan and its routine metadata as one source of truth.
- The meal-plan generation request and `MealPlanDto` have no user-facing plan name. The database has a nullable `template_name` field for templates; it will not be repurposed for user-created plan names.
- The active backend uses PostgreSQL and follows the API → BLL → DAL structure.

## Proposed behavior

### 1. Naming a new plan

- Add an editable **Menu name** field to the creation flow.
- Suggest `Week of dd/MM/yyyy` from the selected start date. If the start date changes, update the suggestion until the user edits it; preserve a manually edited name thereafter.
- Trim names before saving. Accept 1–100 characters; reject blank or over-limit names with a clear English validation message.
- Include the name in the existing generate request. If an older client omits the name, the backend supplies the same date-based default so the existing API contract remains backward-compatible.
- Plans automatically created by a goal-plan revision/continuation use the date-based default for their new start date; users can rename them afterward through the saved-plan rename action.

### 2. Renaming a saved plan

- Show the selected plan name near its calendar and show names in the saved-plan selector alongside the date range.
- Provide a rename action for the selected plan. It opens an inline edit state with Save and Cancel, validates the same 1–100 character limit, and reports failures without losing the current selection.
- Add `PATCH /api/MealPlans/{id}/name` with a request containing the new name. The endpoint requires authentication, verifies that the plan belongs to the caller and is not a template, and follows the existing meal-plan access policy. Return the updated `MealPlanDto`.
- Update both the selected plan and the saved-plan list from the successful response; do not reload or regenerate the menu.

### 3. Persisted name and older plans

- Add a dedicated nullable `name` column to `meal_plans` (maximum 100 characters). It remains nullable for template rows and compatibility with existing records.
- Backfill existing non-template plans with `Week of dd/MM/yyyy` when a start date exists, otherwise `Weekly menu #<id>`.
- Add `Name` to the entity, generation request, response DTO, and mapping. Newly generated non-template plans always persist a trimmed, non-empty name.
- Add the PostgreSQL migration source. Applying it to the shared Supabase database is a separate operation and requires explicit user approval after review.

### 4. Selected-plan context card

- Pass the full selected `plan` to `MenuJourney`, not only its `menu` snapshot.
- For a saved plan, derive its name, goal mode, target weight/date, plan start date, calorie target, split, diet, allergies, profile-at-generation, and selected dishes from that plan's persisted snapshot and routine metadata.
- Do not substitute the current health profile or creation-form defaults when showing a historical plan. If legacy snapshot data is absent, display an explicit “Not recorded” value.
- Keep the algorithm explanation and evidence links static; they describe product behavior and sources rather than a particular plan.
- Selecting another saved plan must update the card and selected title with that plan's values.

## Data flow

1. The creation form sends the chosen plan name with the existing generation request.
2. The BLL trims/validates the name and persists it with the new `MealPlan` through the existing repository flow.
3. List/detail responses include the name and existing menu/routine snapshots.
4. The FE renders the selected plan's name and passes that same DTO to `MenuJourney`.
5. Rename submits only the new name; the BE updates the caller-owned plan and returns its updated DTO.

## Validation and access rules

- Generation name: trimmed, required after backend defaulting, maximum 100 characters.
- Rename name: trimmed, required, maximum 100 characters.
- A caller can rename only their own non-template plan. A missing or non-owned plan is returned as not found, consistent with existing ownership checks.
- Apply existing entitlement checks for viewing/renaming saved plans; do not grant access to expired or otherwise inaccessible plans through the rename endpoint.
- Do not change meal selection, calorie calculations, safety checks, tracking, reminder, payment, or notification behavior.

## Compatibility and migration

- Existing clients can continue generating plans without a name; the backend supplies a default.
- Existing plans receive a stable backfilled name where possible. DTO fallback behavior remains available for any record with a null name.
- The schema migration adds a column and backfills rows; it does not delete or rewrite plan snapshots, schedules, or routine entries.
- Do not apply the migration to Supabase as part of local code implementation without separate approval.

## Acceptance criteria

1. A user can accept or edit the suggested name before creating a plan; the saved plan and API response contain that name.
2. A user can rename a saved plan. The name updates in the selector and the selected-plan area without changing meals or tracking state.
3. A different saved plan selection updates all plan-specific values in “From your profile to your plate” from that plan's snapshot/routine.
4. Updating the user's health profile later does not change the historical plan context shown for an already saved plan.
5. Automatically created continuation/revision plans receive a meaningful date-based name.
6. Old plans remain readable and have a meaningful display name after migration or fallback.
7. A user cannot rename another user's plan or a template.
8. The migration, BLL/API behavior, and FE behavior are verified; no commit or push is made unless requested.

## Implementation boundaries

Expected touch points are the meal-plan entity and PostgreSQL mapping/migration, generation and rename DTOs, meal-planning repository/service, `MealPlansController`, FE meal-plan service, `PlanSetupFlow`, `WeeklyMealPlanner`, `MenuJourney`, and focused existing tests. Preserve unrelated local modifications, especially current meal-planning edits, and inspect the full working-tree diff before changing overlapping files.

The shared Supabase migration is explicitly out of scope until separately approved.
