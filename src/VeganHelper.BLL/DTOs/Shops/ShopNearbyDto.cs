namespace VeganHelper.BLL.DTOs.Shops;

public class ShopNearbyDto
{
    public long ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double DistanceKm { get; set; }
    public decimal Rating { get; set; }
    public bool IsOpenNow { get; set; }
}
