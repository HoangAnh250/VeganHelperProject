using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.DTOs.Shops;

namespace VeganHelper.BLL.Integrations.GooglePlaces;

public class GooglePlacesClient : IGooglePlacesClient
{
    private readonly HttpClient _httpClient;
    private readonly GooglePlacesOptions _options;
    private readonly ILogger<GooglePlacesClient> _logger;

    public GooglePlacesClient(HttpClient httpClient, IOptions<GooglePlacesOptions> options, ILogger<GooglePlacesClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<GooglePlaceDto>> SearchNearbyVeganShopsAsync(decimal latitude, decimal longitude, double radiusMeters)
    {
        var requestBody = new
        {
            includedTypes = new[] { "vegan_restaurant", "vegetarian_restaurant" },
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = radiusMeters
                }
            }
        };

        return await ExecuteSearchAsync("https://places.googleapis.com/v1/places:searchNearby", requestBody);
    }

    public async Task<List<GooglePlaceDto>> SearchVeganShopsByKeywordAsync(string keyword, decimal? latitude = null, decimal? longitude = null, double radiusMeters = 5000)
    {
        var fullKeyword = string.IsNullOrWhiteSpace(keyword) ? "vegan" : $"{keyword} vegan vegetarian";
        
        var requestBody = new Dictionary<string, object>
        {
            { "textQuery", fullKeyword }
        };

        if (latitude.HasValue && longitude.HasValue)
        {
            requestBody["locationBias"] = new
            {
                circle = new
                {
                    center = new { latitude = latitude.Value, longitude = longitude.Value },
                    radius = radiusMeters
                }
            };
        }

        return await ExecuteSearchAsync("https://places.googleapis.com/v1/places:searchText", requestBody);
    }

    public async Task<GooglePlaceDto?> GetPlaceDetailsAsync(string placeId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://places.googleapis.com/v1/places/{placeId}");
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "id,displayName,formattedAddress,location,rating,userRatingCount,currentOpeningHours,photos");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var place = json.RootElement;
            
            return MapToGooglePlaceDto(place);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get place details for {PlaceId} from Google Places API", placeId);
            return null;
        }
    }

    public string GetPhotoUrl(string photoReference, int maxWidth = 400)
    {
        if (string.IsNullOrWhiteSpace(photoReference))
            return string.Empty;

        return $"https://places.googleapis.com/v1/{photoReference}/media?key={_options.ApiKey}&maxHeightPx={maxWidth}&maxWidthPx={maxWidth}";
    }

    private async Task<List<GooglePlaceDto>> ExecuteSearchAsync(string url, object requestBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.formattedAddress,places.location,places.rating,places.userRatingCount,places.currentOpeningHours,places.photos");
        request.Content = JsonContent.Create(requestBody);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = new List<GooglePlaceDto>();
            
            var json = JsonDocument.Parse(content);
            if (json.RootElement.TryGetProperty("places", out var placesArray))
            {
                foreach (var place in placesArray.EnumerateArray())
                {
                    var dto = MapToGooglePlaceDto(place);
                    if (dto != null)
                        result.Add(dto);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search places from Google Places API. URL: {Url}", url);
            return new List<GooglePlaceDto>();
        }
    }

    private GooglePlaceDto? MapToGooglePlaceDto(JsonElement place)
    {
        try
        {
            var dto = new GooglePlaceDto();
            
            if (place.TryGetProperty("id", out var idProp))
                dto.PlaceId = idProp.GetString() ?? string.Empty;
                
            if (place.TryGetProperty("displayName", out var displayNameProp) && displayNameProp.TryGetProperty("text", out var textProp))
                dto.Name = textProp.GetString() ?? string.Empty;
                
            if (place.TryGetProperty("formattedAddress", out var addressProp))
                dto.Address = addressProp.GetString();
                
            if (place.TryGetProperty("location", out var locationProp))
            {
                if (locationProp.TryGetProperty("latitude", out var latProp))
                    dto.Latitude = latProp.GetDecimal();
                if (locationProp.TryGetProperty("longitude", out var lngProp))
                    dto.Longitude = lngProp.GetDecimal();
            }
            
            if (place.TryGetProperty("rating", out var ratingProp))
                dto.Rating = ratingProp.GetDecimal();
                
            if (place.TryGetProperty("userRatingCount", out var ratingCountProp))
                dto.UserRatingsTotal = ratingCountProp.GetInt32();
                
            if (place.TryGetProperty("currentOpeningHours", out var hoursProp) && hoursProp.TryGetProperty("openNow", out var openNowProp))
                dto.IsOpenNow = openNowProp.GetBoolean();
                
            if (place.TryGetProperty("photos", out var photosProp) && photosProp.GetArrayLength() > 0)
            {
                var firstPhoto = photosProp[0];
                if (firstPhoto.TryGetProperty("name", out var nameProp))
                {
                    dto.PhotoReference = nameProp.GetString();
                }
            }
            
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map Google Place JSON to DTO");
            return null;
        }
    }
}
