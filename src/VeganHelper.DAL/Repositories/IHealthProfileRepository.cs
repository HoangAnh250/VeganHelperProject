using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IHealthProfileRepository
{
    Task<HealthProfileProjection> GetAsync(long userId, CancellationToken ct);
    Task UpdateAsync(long userId, UserProfile values, decimal bmi, CancellationToken ct);
    Task ReplaceAllergiesAsync(long userId, IReadOnlyCollection<long> ingredientIds, IReadOnlyCollection<string> customNames, CancellationToken ct);
    Task<DatabasePage<BmiHistory>> GetHistoryAsync(long userId, DateTime since, int pageIndex, int pageSize, CancellationToken ct);
    Task<DatabasePage<Ingredient>> SearchIngredientsAsync(string? keyword, int pageIndex, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<AllergyProjection>?> GetPostWarningsAsync(long userId, long postId, CancellationToken ct);
}
