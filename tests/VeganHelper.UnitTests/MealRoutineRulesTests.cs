using VeganHelper.BLL.Services.MealPlanning;
using VeganHelper.DAL.Entities;

namespace VeganHelper.UnitTests;

public sealed class MealRoutineRulesTests
{
    [Fact]
    public void UnrecordedMeals_CountAsNotFollowed()
    {
        var entries=Enumerable.Range(0,9).Select(i=>new MealPlanEntry {Status=i<3?"skipped":"unlogged"}).ToList();
        Assert.True(MealRoutineReview.NeedsProfileReview(entries));
    }
    [Fact]
    public void SixLoggedMeals_WithLowAdherence_RecommendProfileReview()
    {
        var entries=Enumerable.Range(0,9).Select(i=>new MealPlanEntry {Status=i<3?"followed":i<6?"changed":"unlogged"}).ToList();
        Assert.True(MealRoutineReview.NeedsProfileReview(entries));
    }
    [Theory]
    [InlineData(0,true)]
    [InlineData(6,true)]
    [InlineData(7,false)]
    [InlineData(9,false)]
    public void ThreeDayReview_UsesAllNineScheduledMeals(int followed,bool expected)
    {
        var entries=Enumerable.Range(0,9).Select(i=>new MealPlanEntry {Status=i<followed?"followed":"unlogged"});
        Assert.Equal(expected,MealRoutineReview.NeedsProfileReview(entries));
    }
    [Fact]
    public void EmptySchedule_DoesNotRecommendReview()=>Assert.False(MealRoutineReview.NeedsProfileReview([]));
    [Fact]
    public void LegacyStates_AreNotFollowed()=>Assert.True(MealRoutineReview.NeedsProfileReview(
        [new() {Status="changed"},new() {Status="skipped"},new() {Status="unlogged"}]));
    [Fact]
    public void TimeZone_UsesLocalDateAndConvertsReminderToUtc()
    {
        Assert.Equal(new DateTime(2026,10,17,0,30,0,DateTimeKind.Utc),MealRoutineService.Due(new(2026,10,17),new(7,30),"Asia/Ho_Chi_Minh"));
    }
    [Fact]
    public void WindowsTimeZone_IsNormalizedForBrowserCompatibility()=>
        Assert.Equal("Asia/Bangkok",MealRoutineService.CanonicalZone("SE Asia Standard Time"));
}
