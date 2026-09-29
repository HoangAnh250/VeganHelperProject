using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.DAL.Data;

namespace VeganHelper.BLL.Services;

public class ShopService : IShopService
{
    private readonly AppDbContext _dbContext;

    public ShopService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ShopNearbyDto>> GetNearbyShopsAsync(GetNearbyShopsRequest request)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var userLocation = geometryFactory.CreatePoint(new Coordinate(request.Lng, request.Lat));

        double[] radiusTiers = { request.RadiusKm, 10.0, 20.0 };
        List<ShopNearbyDto> results = new();

        foreach (var radius in radiusTiers)
        {
            var shops = await _dbContext.Shops
                .Where(s => s.Location != null && s.IsApproved && !s.IsDeleted && s.Location.Distance(userLocation) <= radius * 1000)
                .OrderBy(s => s.Location.Distance(userLocation))
                .Select(s => new ShopNearbyDto
                {
                    ShopId = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    DistanceKm = s.Location!.Distance(userLocation) / 1000,
                    Rating = s.Rating,
                    IsOpenNow = CheckIsOpen(s.OpeningHours)
                })
                .ToListAsync();

            if (shops.Any())
            {
                results = shops;
                break;
            }
        }

        return results;
    }

    private static bool CheckIsOpen(string? openingHours)
    {
        if (string.IsNullOrEmpty(openingHours)) return true; // Mặc định mở nếu không rõ
        return true; // Simplified
    }
}
