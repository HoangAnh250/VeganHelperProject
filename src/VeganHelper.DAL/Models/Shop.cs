namespace VeganHelper.DAL.Models;

using NetTopologySuite.Geometries;

public sealed class Shop
{
    public long Id { get; set; }
    public long? SuggestedByUserId { get; set; }
    public string? GooglePlaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Point? Location { get; set; }
    public string? OpeningHours { get; set; }
    public decimal Rating { get; set; }
    public bool IsApproved { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
