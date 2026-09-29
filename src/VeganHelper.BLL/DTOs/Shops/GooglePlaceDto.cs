namespace VeganHelper.BLL.DTOs.Shops;

public class GooglePlaceDto
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public bool? IsOpenNow { get; set; }
    public string? PhotoReference { get; set; }
}
