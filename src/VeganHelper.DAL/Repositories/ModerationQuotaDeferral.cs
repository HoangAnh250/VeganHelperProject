namespace VeganHelper.DAL.Repositories;

public static class ModerationQuotaDeferral
{
    public static bool AppliesTo(string? deferredModel, string configuredModel) =>
        string.IsNullOrWhiteSpace(deferredModel) ||
        string.Equals(deferredModel, configuredModel, StringComparison.OrdinalIgnoreCase);
}
