namespace VeganHelper.DAL.Entities;

// ISO weekday: Monday=1, Sunday=7. Overnight periods close on the following day.
public sealed class ShopOpeningPeriod
{
    public long Id { get; set; }
    public long ShopId { get; set; }
    public byte DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class ShopMedia
{
    public long Id { get; set; }
    public long ShopId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class ShopMenuItem
{
    public long Id { get; set; }
    public long ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
}
