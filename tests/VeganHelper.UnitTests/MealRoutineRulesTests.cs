using VeganHelper.BLL.Services.MealPlanning;
using VeganHelper.DAL.Entities;

namespace VeganHelper.UnitTests;

public sealed class MealRoutineRulesTests
{
    [Fact]
    public void IncompleteLogging_DoesNotBecomeSkippedMeals()
    {
        var entries=Enumerable.Range(0,9).Select(i=>new MealPlanEntry {Status=i<3?"skipped":"unlogged"}).ToList();
        Assert.False(MealRoutineReview.NeedsProfileReview(entries));
    }
    [Fact]
    public void SixLoggedMeals_WithLowAdherence_RecommendProfileReview()
    {
        var entries=Enumerable.Range(0,9).Select(i=>new MealPlanEntry {Status=i<3?"followed":i<6?"changed":"unlogged"}).ToList();
        Assert.True(MealRoutineReview.NeedsProfileReview(entries));
    }
    [Fact]
    public void TimeZone_UsesLocalDateAndConvertsReminderToUtc()
    {
        Assert.Equal(new DateTime(2026,10,17,0,30,0,DateTimeKind.Utc),MealRoutineService.Due(new(2026,10,17),new(7,30),"Asia/Ho_Chi_Minh"));
    }
    [Fact]
    public void WindowsTimeZone_IsNormalizedForBrowserCompatibility()=>
        Assert.Equal("Asia/Bangkok",MealRoutineService.CanonicalZone("SE Asia Standard Time"));
}
