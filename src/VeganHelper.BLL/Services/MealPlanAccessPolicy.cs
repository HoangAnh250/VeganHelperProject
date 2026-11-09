using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services;

public static class MealPlanAccessPolicy
{
    public static void RequireGeneration(MealPlanAccessDto access)
    {
        if (access.CanGenerate) return;
        if (access.Status is "grace_period" or "expired")
            throw new VeganHelper.BLL.Exceptions.MealPlanAccessRequiredException(
                access.Status == "grace_period"
                    ? "Your package has expired. You can finish your current weekly menu, but please renew your package before creating another week."
                    : "Your package has expired. Please renew your package to create weekly menus and access saved plans.",
                "MEAL_PLAN_PACKAGE_EXPIRED");
        if (access.Status == "scheduled")
            throw new VeganHelper.BLL.Exceptions.MealPlanAccessRequiredException(
                "Your package has not started yet. Please return when your access becomes active.", "MEAL_PLAN_PACKAGE_NOT_STARTED");
        throw new VeganHelper.BLL.Exceptions.MealPlanAccessRequiredException(
            "Your three free meal-plan trials have been used. Please choose a package to create more weekly menus.",
            "MEAL_PLAN_TRIAL_LIMIT_REACHED");
    }

    public const int ReadOnlyGraceDays = 7;
    public const int TrialGenerationLimit = 3;

    public static MealPlanAccessDto Evaluate(UserMealPlanAccess? latestAccess, bool hasActiveAccess, int trialGenerationsUsed, DateTime now)
    {
        var used=Math.Clamp(trialGenerationsUsed,0,TrialGenerationLimit);
        var remaining=TrialGenerationLimit-used;
        var canViewTrialPlans=used>0;

        if (hasActiveAccess)
        {
            return new MealPlanAccessDto(
                "active",
                CanGenerate: true,
                CanViewExistingPlans: true,
                latestAccess?.ExpiresAt,
                latestAccess?.ExpiresAt.AddDays(ReadOnlyGraceDays),
                remaining,
                CanViewTrialPlans: true);
        }

        if (latestAccess is null)
            return new MealPlanAccessDto("none", remaining>0, false, null, null, remaining, canViewTrialPlans);

        var graceEndsAt = latestAccess.ExpiresAt.AddDays(ReadOnlyGraceDays);
        if (latestAccess.StartsAt <= now && now <= graceEndsAt)
            return new MealPlanAccessDto("grace_period", false, true, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);

        if (latestAccess.StartsAt > now)
            return new MealPlanAccessDto("scheduled", false, false, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);

        return new MealPlanAccessDto("expired", false, false, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);
    }
}
