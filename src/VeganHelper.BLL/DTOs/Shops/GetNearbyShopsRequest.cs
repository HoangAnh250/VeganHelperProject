namespace VeganHelper.BLL.DTOs.Shops;

public class GetNearbyShopsRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double RadiusKm { get; set; } = 5.0;
}
