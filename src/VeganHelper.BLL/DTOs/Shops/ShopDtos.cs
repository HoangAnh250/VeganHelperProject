using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Shops;

public class ShopListDto
{
    public long ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? DistanceKm { get; set; }
    public decimal? Rating { get; set; }
    public bool? IsOpenNow { get; set; }
}

public sealed class ShopDetailDto : ShopListDto
{
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public string? OpeningHours { get; set; }
    public string? GoogleMapsDirectionsUrl { get; set; }
    public List<string> MediaUrls { get; set; } = new();
    public List<string> MenuItems { get; set; } = new();
    public List<ShopOpeningPeriodDto> OpeningPeriods { get; set; } = new();
}

public sealed class ShopOpeningPeriodDto
{
    public byte DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}

public class ShopLocationRequest
{
    [Range(-90d, 90d)]
    public double? Lat { get; set; }
    [Range(-180d, 180d)]
    public double? Lng { get; set; }
}

public sealed class NearbyShopsRequest : ShopLocationRequest
{
    [Range(5d, 10d)]
    public double RadiusKm { get; set; } = 5;
    [Range(1, 1_000_000)]
    public int PageIndex { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class SearchShopsRequest : ShopLocationRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Keyword { get; set; } = string.Empty;
    [Range(1, 1_000_000)]
    public int PageIndex { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
