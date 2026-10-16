using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class ModerationQuotaDeferralTests
{
    [Theory]
    [InlineData("gemini-3.8-flash", "gemini-3.5-flash-lite", false)]
    [InlineData("gemini-3.5-flash-lite", "gemini-3.5-flash-lite", true)]
    [InlineData(null, "gemini-3.5-flash-lite", true)]
    public void AppliesTo_OnlyBlocksTheModelThatHitQuota(
        string? deferredModel, string configuredModel, bool expected)
    {
        Assert.Equal(expected, ModerationQuotaDeferral.AppliesTo(deferredModel, configuredModel));
    }
}
