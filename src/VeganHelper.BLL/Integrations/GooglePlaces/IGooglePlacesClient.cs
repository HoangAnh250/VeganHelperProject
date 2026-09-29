namespace VeganHelper.BLL.Integrations.GooglePlaces;

using VeganHelper.BLL.DTOs.Shops;

public interface IGooglePlacesClient
{
    Task<List<GooglePlaceDto>> SearchNearbyVeganShopsAsync(decimal latitude, decimal longitude, double radiusMeters);
    Task<List<GooglePlaceDto>> SearchVeganShopsByKeywordAsync(string keyword, decimal? latitude = null, decimal? longitude = null, double radiusMeters = 5000);
    Task<GooglePlaceDto?> GetPlaceDetailsAsync(string placeId);
    string GetPhotoUrl(string photoReference, int maxWidth = 400);
}
