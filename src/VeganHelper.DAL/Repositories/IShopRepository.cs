using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class ShopProjection
{
    public Shop Shop { get; set; } = null!;
    public double? DistanceKm { get; set; }
}

public interface IShopRepository
{
    Task<DatabasePage<ShopProjection>> SearchAsync(string? keyword, double? lat, double? lng, double? radiusKm, int pageIndex, int pageSize, CancellationToken ct);
    Task<ShopProjection?> GetDetailAsync(long id, double? lat, double? lng, CancellationToken ct);
}
