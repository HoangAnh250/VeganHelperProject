using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class AllergyProjection
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}

public sealed record HealthProfileProjection(UserProfile? Profile, IReadOnlyList<AllergyProjection> Allergies);
public sealed record DatabasePage<T>(long TotalCount, IReadOnlyList<T> Items);
