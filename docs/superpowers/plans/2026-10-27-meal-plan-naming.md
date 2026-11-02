# Meal Plan Naming and Selected-Plan Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist user-editable meal-plan names, support renaming saved plans, and make “From your profile to your plate” show the selected plan's saved context.

**Architecture:** Keep the existing API → BLL → DAL flow. Add a dedicated nullable PostgreSQL `meal_plans.name` field, include the name in existing generation/list/detail DTOs, and add an owner-scoped rename endpoint. In FE, pass the selected full plan DTO to the context card and keep name/selection state synchronized without regenerating menu data.

**Tech Stack:** ASP.NET Core .NET 10, EF Core 10, Npgsql/PostgreSQL, React 18, Vite 8.

**Spec:** `docs/superpowers/specs/2026-10-27-meal-plan-naming-and-selected-plan-context-design.md`

## Global Constraints

- Keep the backend's API → BLL → DAL layering and DTO contracts.
- User-facing FE copy remains 100% English.
- Plan names are trimmed and limited to 1–100 characters.
- Older generation clients may omit the name; the BE supplies `Week of dd/MM/yyyy` from the plan start date.
- Existing non-template plans are backfilled with a date-based name or `Weekly menu #<id>` when no start date exists.
- Rename only caller-owned, non-template plans and honor existing viewing entitlements.
- Do not apply the migration to shared Supabase without separate explicit approval.
- Preserve existing local modifications; do not commit or push.
- Do not add or run tests in this task; use build checks and focused diff review.

## Review Focus

- Blank, whitespace-only, and over-100-character names must not be persisted. Review FE and BE normalization/validation in Tasks 2 and 3.
- Legacy clients omitting the name and revision-created plans must receive deterministic defaults. Review generation paths in Task 2.
- Legacy plans and templates must remain readable with the correct name/fallback and without changing template semantics. Review migration and DTO mapping in Task 1.
- Attempts to rename another user's plan, a template, or a plan outside current entitlements must not update it. Review owner and access checks in Task 2.
- Switching plans or updating the current health profile must not make the context card display the wrong plan's values. Review selected-plan data flow in Task 4.

---

### Task 1: Add persisted meal-plan name and PostgreSQL migration

**Files:**
- Modify: `src/VeganHelper.DAL/Entities/MealPlan.cs`
- Modify: `src/VeganHelper.DAL/Persistence/AppDbContext.cs`
- Create: `src/VeganHelper.DAL/PostgresMigrations/<timestamp>_AddMealPlanNames.cs` and generated designer
- Modify: `src/VeganHelper.DAL/PostgresMigrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: nullable `MealPlan.Name` mapped to PostgreSQL `meal_plans.name` as `character varying(100)`.

- [x] Add `Name` as a nullable entity property and map it to the dedicated `name` column; leave `TemplateName` and template behavior unchanged.
- [x] Generate a PostgreSQL migration that adds `name`, backfills non-template records from `start_date` (`Week of dd/MM/yyyy`) or falls back to `Weekly menu #<id>`, and leaves template rows nullable.
- [x] Inspect `Up` and `Down` to confirm the migration changes only the new name column and its backfill; do not run `database update` or apply it to Supabase.
- [x] Build the DAL project and inspect the migration/snapshot diff.

### Task 2: Add generation defaults and owner-scoped rename API

**Files:**
- Modify: `src/VeganHelper.BLL/DTOs/MealPlanning/MealPlanningDtos.cs`
- Modify: `src/VeganHelper.BLL/Services/MealPlanning/MealPlanningService.cs`
- Modify: `src/VeganHelper.BLL/Services/MealPlanning/MealRoutineService.cs`
- Modify: `src/VeganHelper.DAL/Repositories/IMealPlanningRepository.cs`
- Modify: `src/VeganHelper.DAL/Repositories/MealPlanningRepository.cs`
- Modify: `src/VeganHelper.API/Controllers/MealPlansController.cs`

**Interfaces:**
- `GenerateMealPlanRequest.Name`: nullable string for backward compatibility; normalize blank/missing to `Week of {startDate:dd/MM/yyyy}` and reject nonblank names over 100 characters.
- `MealPlanDto.Name`: string for normal user plans, with a DTO fallback for legacy nulls.
- `UpdateMealPlanNameRequest(string Name)`.
- `IMealPlanningService.RenameAsync(long userId, long id, UpdateMealPlanNameRequest request, CancellationToken ct) -> Task<MealPlanDto>`.
- `IMealPlanningRepository.UpdateNameAsync(long userId, long id, string name, bool trialOnly, DateTime updatedAt, CancellationToken ct) -> Task<MealPlan?>`.
- `PATCH /api/MealPlans/{id}/name`: authenticated; returns the updated `MealPlanDto`.

- [x] Add `Name` to request/response DTOs and ensure generated/list/detail DTOs expose persisted names.
- [x] Normalize and validate creation names before saving `MealPlan`; default omitted names for older clients.
- [x] Give automatically created goal-plan revision/continuation rows the date-based name for their new `StartDate`.
- [x] Implement repository update filtered by `id`, `userId`, and `!IsTemplate`; set `UpdatedAt` and persist the trimmed name.
- [x] Implement BLL rename validation, existing plan-view entitlement check, and not-found behavior for missing/non-owned/template rows.
- [x] Add the authenticated PATCH controller action and return the updated DTO.
- [x] Build the API project and inspect changed hunks against existing uncommitted work.

### Task 3: Add FE naming, selector labels, and rename interaction

**Files:**
- Modify: `src/services/mealPlanService.js`
- Modify: `src/components/meal-planning/PlanSetupFlow.jsx`
- Modify: `src/components/meal-planning/WeeklyMealPlanner.jsx`
- Modify: `src/components/meal-planning/weekly-menu.css`

**Interfaces:**
- `renameMealPlan(id, name) -> Promise<MealPlanDto>` calls `PATCH /MealPlans/{id}/name`.
- `PlanSetupFlow.onGenerate` includes `name` for both balanced and weight-goal plans.

- [x] Add an editable 1–100-character **Menu name** field. Suggest `Week of dd/MM/yyyy`; update the suggestion on start-date changes until the user edits it.
- [x] Preserve the existing target-date entry mask/calendar changes in `PlanSetupFlow.jsx` while adding the name field.
- [x] Include the trimmed name in the generate request and show it in the review step.
- [x] Add the rename service call and an accessible inline rename/cancel/save state for the selected plan; surface request errors without clearing the selected plan.
- [x] Render each saved plan's name with its date range in the selector. After rename, synchronize both `plan` and the matching item in `saved.items` from the response, without regenerating or reloading the menu.
- [x] Review layout/focus/disabled states and build FE.

### Task 4: Bind the context card to the selected full plan

**Files:**
- Modify: `src/components/meal-planning/WeeklyMealPlanner.jsx`
- Modify: `src/components/meal-planning/MenuJourney.jsx`

**Interfaces:**
- `MenuJourney` consumes `plan: MealPlanDto`; the selected plan supplies `menu`, `routine`, `name`, dates, and saved generation snapshot.

- [x] Pass the selected `plan` to `MenuJourney` and derive its saved menu from `plan.menu`.
- [x] Use that plan's energy profile, diet/allergy snapshots, calorie totals/split, target goal/routine, and meal selection for plan-specific values.
- [x] Do not fall back to the latest/current profile for saved plans; show `Not recorded` when an older plan lacks a snapshot value.
- [x] Show the selected name in the selected-plan context while keeping algorithm explanation and source links static.
- [x] Review selection changes and historical snapshot behavior; build FE.

### Task 5: Final integration review

**Files:**
- Review only the changed implementation files and generated PostgreSQL migration.

- [x] Run `dotnet build src/VeganHelper.API/VeganHelper.API.csproj --no-restore` from the BE repository root.
- [x] Run `npm run build` from `D:\Study\tài liệu\RUSH\SWP391\frontend-VeganHelperSystem`.
- [x] Run `git diff --check` in each repository and inspect that unrelated existing local edits remain intact.
- [x] Report the migration file and clearly state that shared Supabase was not changed; do not commit or push.
