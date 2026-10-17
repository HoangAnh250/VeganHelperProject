using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services;

public static class MealPlanAccessPolicy
{
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
            return new MealPlanAccessDto("grace_period", remaining>0, true, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);

        if (latestAccess.StartsAt > now)
            return new MealPlanAccessDto("scheduled", remaining>0, false, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);

        return new MealPlanAccessDto("expired", remaining>0, false, latestAccess.ExpiresAt, graceEndsAt, remaining, canViewTrialPlans);
    }
}
