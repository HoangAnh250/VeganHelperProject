namespace VeganHelper.BLL.DTOs.Shops;

public class ShopDto
{
    public long ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal Rating { get; set; }
    public int UserRatingsTotal { get; set; }
    public bool? IsOpenNow { get; set; }
    public string? PhotoUrl { get; set; }
    public string? GooglePlaceId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class SearchShopsResponse
{
    public int StatusCode { get; set; } = 200;
    public List<ShopDto> Shops { get; set; } = new();
    public PaginationDto Pagination { get; set; } = new();
}

public class PaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
}
