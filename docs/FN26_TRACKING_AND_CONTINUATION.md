# FN26 — Tracking and Completed-Week Check-In

## Tracking contract

Only weight-goal plans use tracking. The current API and UI expose `followed` and `unlogged` (Not recorded). Historical `changed` and `skipped` values are treated as Not recorded; older clients submitting these values are normalized to `unlogged`. No legacy rows are destructively rewritten.

After each completed three-day block in the plan timezone, the worker evaluates all scheduled entries in that block. Future blocks are excluded. Fewer than 70% marked `followed` results in one health-profile check-in notification per root plan and block. There is no minimum recorded-meal threshold. Calendar gaps in late continuations are not treated as scheduled meals. A check-in does not infer a particular food choice or measured weight change, force a profile update, or automatically replace a menu.

The Hangfire job `meal-routine-review` is registered every minute when `MealPlanning__WorkerEnabled=true` and background jobs are permitted. Multiple sweeps do not duplicate the same check-in event.

## Completed-week contract

Starting the local day after `end_date`, the selected menu displays a completed-week check-in instead of the calendar. Users may view the old calendar, defer planning, or continue. A balanced menu offers a new balanced week without weight tracking.

A weight-goal continuation requires a fresh current weight and eligibility confirmation. Target weight and target date default to the original values. The user may revise them if circumstances have changed. The start date defaults to today after a late return and can be moved forward; it cannot precede today or overlap another menu. Early continuation before the original week ends is rejected.

Preview recalculates TDEE, calorie adjustment, goal safety and catalog suitability without persisting a weight update. Confirmation rechecks ownership, access, routine version, profile fingerprint, the proposed weight, safety and overlapping dates. The new plan, routine, entries, current profile weight/BMI and BMI-history record are committed together. Any failure rolls the transaction back. The original week's snapshot and tracking remain in history. A completed source routine becomes `continued`; its DTO supplies `nextPlanId` for opening its existing continuation. Concurrent duplicate confirmations are rejected.

Reaching or passing the original goal prompts guidance toward maintenance or a new goal. An expired or insufficient goal deadline requires revision before a complete new week can be generated. No database schema migration is required.

## Fast verification without waiting

From the backend repository on drive D, run:

```powershell
dotnet test tests/VeganHelper.UnitTests/VeganHelper.UnitTests.csproj --filter FullyQualifiedName~MealRoutineRulesTests
dotnet test tests/VeganHelper.IntegrationTests/VeganHelper.IntegrationTests.csproj --filter FullyQualifiedName~MealPlanningApiTests
```

Integration tests use a temporary PostgreSQL 17 container through Docker. An explicitly configured test connection is accepted only for a local database named `member2_test...`; shared Supabase connections are rejected by the fixture. Tests prepare past-dated plans and entries and directly execute the review service, so three days and seven days need not elapse in real time. No system-clock changes are required.

Coverage includes zero confirmations, the 6/9 versus 7/9 adherence boundary, repeat sweeps, cross-week review, exclusion of balanced plans, day-eight behavior, late returns, fresh weight and BMI-history persistence, trial generation limits, unsafe goals, reached goals, expired deadlines, overlapping menus, foreign ownership, changed preview inputs and concurrent confirmation.

From the frontend repository:

```powershell
npm test -- src/components/meal-planning src/pages/member/WeeklyMenu.test.jsx
npm run build
```

Frontend tests use a simulated date and mocked API responses. They verify the local day-seven/day-eight boundary, historical calendar access, fresh-weight entry, original-goal defaults, confirmation only after an allowed preview, invalidation after edits, and opening an existing next week.
