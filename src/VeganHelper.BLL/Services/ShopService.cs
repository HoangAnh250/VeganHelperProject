using Microsoft.EntityFrameworkCore;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Integrations.GooglePlaces;
using VeganHelper.DAL.Data;
using VeganHelper.DAL.Models;

namespace VeganHelper.BLL.Services;

public class ShopService : IShopService
{
    private readonly AppDbContext _context;
    private readonly IGooglePlacesClient _googlePlacesClient;

    public ShopService(AppDbContext context, IGooglePlacesClient googlePlacesClient)
    {
        _context = context;
        _googlePlacesClient = googlePlacesClient;
    }

    public async Task<SearchShopsResponse> SearchShopsAsync(string keyword, decimal? latitude, decimal? longitude, int pageIndex = 1, int pageSize = 10)
    {
        List<GooglePlaceDto> googlePlaces;
        
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            googlePlaces = await _googlePlacesClient.SearchVeganShopsByKeywordAsync(keyword, latitude, longitude);
        }
        else if (latitude.HasValue && longitude.HasValue)
        {
            googlePlaces = await _googlePlacesClient.SearchNearbyVeganShopsAsync(latitude.Value, longitude.Value, 5000); // 5km default
        }
        else
        {
            googlePlaces = new List<GooglePlaceDto>();
        }

        // Upsert to local database to get ShopId
        var resultShops = new List<ShopDto>();
        
        foreach (var gp in googlePlaces)
        {
            var shop = await _context.Set<Shop>().FirstOrDefaultAsync(s => s.GooglePlaceId == gp.PlaceId);
            
            if (shop == null)
            {
                shop = new Shop
                {
                    GooglePlaceId = gp.PlaceId,
                    Name = gp.Name,
                    Address = gp.Address,
                    Latitude = gp.Latitude,
                    Longitude = gp.Longitude,
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<Shop>().Add(shop);
            }
            else
            {
                // Update basic info in case it changed
                shop.Name = gp.Name;
                shop.Address = gp.Address;
                shop.Latitude = gp.Latitude;
                shop.Longitude = gp.Longitude;
            }
            
            await _context.SaveChangesAsync();
            
            decimal distance = 0;
            if (latitude.HasValue && longitude.HasValue)
            {
                distance = CalculateDistance(latitude.Value, longitude.Value, gp.Latitude, gp.Longitude);
            }
            
            resultShops.Add(new ShopDto
            {
                ShopId = shop.Id,
                GooglePlaceId = gp.PlaceId,
                Name = gp.Name,
                Address = gp.Address,
                DistanceKm = Math.Round(distance, 1),
                Rating = gp.Rating ?? 0,
                UserRatingsTotal = gp.UserRatingsTotal ?? 0,
                IsOpenNow = gp.IsOpenNow,
                Latitude = gp.Latitude,
                Longitude = gp.Longitude,
                PhotoUrl = _googlePlacesClient.GetPhotoUrl(gp.PhotoReference ?? string.Empty)
            });
        }

        // Sort by Rating and Distance
        resultShops = resultShops
            .OrderByDescending(s => s.Rating)
            .ThenBy(s => s.DistanceKm)
            .ToList();

        var totalCount = resultShops.Count;
        var pagedShops = resultShops.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

        return new SearchShopsResponse
        {
            StatusCode = 200,
            Shops = pagedShops,
            Pagination = new PaginationDto
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            }
        };
    }

    private decimal CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var d1 = (double)lat1 * (Math.PI / 180.0);
        var num1 = (double)lon1 * (Math.PI / 180.0);
        var d2 = (double)lat2 * (Math.PI / 180.0);
        var num2 = (double)lon2 * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
        
        var distance = 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3))); // In meters
        return (decimal)(distance / 1000.0); // Convert to km
    }
}
