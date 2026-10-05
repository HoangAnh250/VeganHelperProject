using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class ShopRepository(AppDbContext db) : IShopRepository
{
    private IQueryable<ShopProjection> Query(double? lat, double? lng, bool detail = false)
    {
        var latitude = lat ?? 0;
        var longitude = lng ?? 0;
        const double radians = Math.PI / 180;
        var shops = db.Shops.AsNoTracking().Include(s => s.OpeningPeriods).AsSplitQuery()
            .Where(s => s.IsApproved && !s.IsDeleted);
        if (detail) shops = shops.Include(s => s.Media).Include(s => s.MenuItems);
        return shops.Select(s => new ShopProjection
            {
                Shop = s,
                DistanceKm = lat.HasValue && lng.HasValue && s.Latitude.HasValue && s.Longitude.HasValue
                    ? 12742d * Math.Asin(Math.Min(1d, Math.Sqrt(
                        Math.Pow(Math.Sin(((double)s.Latitude!.Value - latitude) * radians / 2), 2)
                        + Math.Cos(latitude * radians) * Math.Cos((double)s.Latitude.Value * radians)
                        * Math.Pow(Math.Sin(((double)s.Longitude!.Value - longitude) * radians / 2), 2))))
                    : (double?)null
            });
    }

    public async Task<DatabasePage<ShopProjection>> SearchAsync(string? keyword, double? lat, double? lng, double? radiusKm, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Query(lat, lng);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.ToLowerInvariant();
            query = query.Where(s => s.Shop.Name.ToLower().Contains(normalized)
                || (s.Shop.Address != null && s.Shop.Address.ToLower().Contains(normalized))
                || s.Shop.MenuItems.Any(m => m.Name.ToLower().Contains(normalized)));
        }
        if (radiusKm.HasValue) query = query.Where(s => s.DistanceKm.HasValue && s.DistanceKm.Value <= radiusKm.Value);
        var total = await query.LongCountAsync(ct);
        var ordered = radiusKm.HasValue
            ? query.OrderBy(s => s.DistanceKm).ThenByDescending(s => s.Shop.Rating.HasValue).ThenByDescending(s => s.Shop.Rating).ThenBy(s => s.Shop.Id)
            : query.OrderByDescending(s => s.Shop.Rating.HasValue).ThenByDescending(s => s.Shop.Rating)
                .ThenBy(s => !s.DistanceKm.HasValue).ThenBy(s => s.DistanceKm).ThenBy(s => s.Shop.Name).ThenBy(s => s.Shop.Id);
        var items = await ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(total, items);
    }

    public Task<ShopProjection?> GetDetailAsync(long id, double? lat, double? lng, CancellationToken ct) =>
        Query(lat, lng, detail: true).Where(s => s.Shop.Id == id)
            .SingleOrDefaultAsync(ct);
}
