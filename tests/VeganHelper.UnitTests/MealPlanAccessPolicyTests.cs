using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;

namespace VeganHelper.UnitTests;

public sealed class MealPlanAccessPolicyTests
{
    [Fact]
    public void NoPurchase_OffersTrialButBlocksPaidViewing()
    {
        var result = MealPlanAccessPolicy.Evaluate(null, false, 0, Utc(2026, 10, 17));

        Assert.Equal("none", result.Status);
        Assert.True(result.CanGenerate);
        Assert.False(result.CanViewExistingPlans);
        Assert.Equal(3, result.TrialGenerationsRemaining);
        Assert.False(result.CanViewTrialPlans);
    }

    [Fact]
    public void ExhaustedTrial_BlocksGenerationButKeepsTrialPlansReadable()
    {
        var result = MealPlanAccessPolicy.Evaluate(null, false, 3, Utc(2026, 10, 17));

        Assert.Equal("none", result.Status);
        Assert.False(result.CanGenerate);
        Assert.False(result.CanViewExistingPlans);
        Assert.Equal(0, result.TrialGenerationsRemaining);
        Assert.True(result.CanViewTrialPlans);
    }

    [Fact]
    public void ActivePurchase_AllowsGenerationAndViewing()
    {
        var now = Utc(2026, 10, 17);
        var result = MealPlanAccessPolicy.Evaluate(Access(now.AddDays(-2), now.AddDays(5)), true, 1, now);

        Assert.Equal("active", result.Status);
        Assert.True(result.CanGenerate);
        Assert.True(result.CanViewExistingPlans);
        Assert.Equal(2, result.TrialGenerationsRemaining);
    }

    [Fact]
    public void ExpiredPurchase_AllowsOnlyReadAccessWithinSevenDayGrace()
    {
        var now = Utc(2026, 10, 17);
        var result = MealPlanAccessPolicy.Evaluate(Access(now.AddDays(-20), now.AddDays(-3)), false, 0, now);

        Assert.Equal("grace_period", result.Status);
        Assert.True(result.CanGenerate);
        Assert.True(result.CanViewExistingPlans);
        Assert.Equal(3, result.TrialGenerationsRemaining);
        Assert.Equal(now.AddDays(4), result.GraceEndsAt);
    }

    [Fact]
    public void ExpiredGrace_BlocksViewing()
    {
        var now = Utc(2026, 10, 17);
        var result = MealPlanAccessPolicy.Evaluate(Access(now.AddDays(-30), now.AddDays(-8)), false, 3, now);

        Assert.Equal("expired", result.Status);
        Assert.False(result.CanGenerate);
        Assert.False(result.CanViewExistingPlans);
        Assert.True(result.CanViewTrialPlans);
    }

    [Fact]
    public void FutureRenewal_IsNotTreatedAsCurrentAccess()
    {
        var now = Utc(2026, 10, 17);
        var result = MealPlanAccessPolicy.Evaluate(Access(now.AddDays(1), now.AddMonths(1)), false, 0, now);

        Assert.Equal("scheduled", result.Status);
        Assert.True(result.CanGenerate);
        Assert.False(result.CanViewExistingPlans);
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static UserMealPlanAccess Access(DateTime startsAt, DateTime expiresAt) => new()
    {
        StartsAt = startsAt,
        ExpiresAt = expiresAt
    };
}
