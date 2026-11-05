using VeganHelper.BLL.Services.MealPlanning;
using VeganHelper.DAL.Entities;

namespace VeganHelper.UnitTests;

public sealed class WeightGoalSafetyTests
{
    private static UserProfile Profile() => new() { HeightCm=170, WeightKg=70, BirthDate=new(1996,1,1), BiologicalSex="male", ActivityLevel="moderate", DietType="vegan" };
    private static readonly DateOnly Today=new(2026,10,17);

    [Fact]
    public void GradualLoss_UsesCurrentTdeeAndRemainingDays()
    {
        var result=WeightGoalSafety.Evaluate(Profile(),Today,68,Today.AddDays(70),true);
        Assert.True(result.Allowed);
        Assert.Equal(-220m,result.DailyAdjustment);
        Assert.Equal(result.Tdee-220m,result.TargetCalories);
    }
    [Fact]
    public void ShortenedDeadline_RejectsCatchUpRestriction()
    {
        var result=WeightGoalSafety.Evaluate(Profile(),Today,68,Today.AddDays(7),true);
        Assert.False(result.Allowed);
        Assert.NotEmpty(result.Reasons);
    }
    [Theory]
    [InlineData(40,70)]
    [InlineData(90,365)]
    [InlineData(68,0)]
    [InlineData(68,-1)]
    public void UnsafeBmiOrExpiredDeadline_IsRejected(decimal target,int days) =>
        Assert.False(WeightGoalSafety.Evaluate(Profile(),Today,target,Today.AddDays(days),true).Allowed);

    [Fact]
    public void ExplicitEligibilityConfirmation_IsRequired() =>
        Assert.False(WeightGoalSafety.Evaluate(Profile(),Today,68,Today.AddDays(70),false).Allowed);

    [Fact]
    public void ExtremeTarget_IsRejectedWithoutDecimalOverflow() =>
        Assert.False(WeightGoalSafety.Evaluate(Profile(),Today,decimal.MaxValue,Today.AddDays(70),true).Allowed);

    [Fact]
    public void Gain_UsesSurplusAndRejectsExcess()
    {
        Assert.True(WeightGoalSafety.Evaluate(Profile(),Today,71,Today.AddDays(70),true).Allowed);
        Assert.False(WeightGoalSafety.Evaluate(Profile(),Today,71,Today.AddDays(7),true).Allowed);
    }

    [Fact]
    public void CurrentBmi_Exactly40_PassesInitialBoundaryCheck_AndEvaluatesGoal()
    {
        // Height 150 cm, Weight 90 kg -> exactly BMI 40.0 (90 / 1.5^2 = 90 / 2.25 = 40.0)
        var profile = new UserProfile
        {
            HeightCm = 150,
            WeightKg = 90,
            BirthDate = new(1996, 1, 1),
            BiologicalSex = "male",
            ActivityLevel = "moderate",
            DietType = "vegan"
        };
        // Staged weight loss: target 88 kg in 70 days (deficit ~220 kcal/day, rate 0.2 kg/week)
        var result = WeightGoalSafety.Evaluate(profile, Today, 88, Today.AddDays(70), true);
        Assert.True(result.Allowed);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void CurrentBmi_Above40_IsRejectedWithClinicalGuidance()
    {
        // Height 150 cm, Weight 90.1 kg -> BMI 40.044... (> 40.0)
        var profile = new UserProfile
        {
            HeightCm = 150,
            WeightKg = 90.1m,
            BirthDate = new(1996, 1, 1),
            BiologicalSex = "male",
            ActivityLevel = "moderate",
            DietType = "vegan"
        };
        var result = WeightGoalSafety.Evaluate(profile, Today, 88, Today.AddDays(70), true);
        Assert.False(result.Allowed);
        Assert.Contains(result.Reasons, r => r.Contains("Weekly Menu cannot assess those needs closely enough"));
    }

    [Fact]
    public void CurrentBmi_Below18_5_IsRejectedWithClinicalGuidance()
    {
        // Height 170 cm, Weight 50 kg -> BMI ~17.3 (< 18.5)
        var profile = new UserProfile
        {
            HeightCm = 170,
            WeightKg = 50,
            BirthDate = new(1996, 1, 1),
            BiologicalSex = "male",
            ActivityLevel = "moderate",
            DietType = "vegan"
        };
        var result = WeightGoalSafety.Evaluate(profile, Today, 48, Today.AddDays(70), true);
        Assert.False(result.Allowed);
        Assert.Contains(result.Reasons, r => r.Contains("Given your current measurements"));
    }
}
