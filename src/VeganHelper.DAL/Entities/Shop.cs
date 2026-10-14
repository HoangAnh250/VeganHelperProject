namespace VeganHelper.DAL.Entities;

public sealed class Shop
{
    public long Id { get; set; }
    public long? SuggestedByUserId { get; set; }
    public string? GooglePlaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public decimal? Rating { get; set; }
    public string? OpeningHours { get; set; }
    public ICollection<ShopOpeningPeriod> OpeningPeriods { get; set; } = new List<ShopOpeningPeriod>();
    public ICollection<ShopMedia> Media { get; set; } = new List<ShopMedia>();
    public ICollection<ShopMenuItem> MenuItems { get; set; } = new List<ShopMenuItem>();
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsApproved { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
