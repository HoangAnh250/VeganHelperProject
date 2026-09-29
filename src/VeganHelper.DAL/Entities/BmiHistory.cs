namespace VeganHelper.DAL.Entities;

public sealed class BmiHistory
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public decimal BmiValue { get; set; }
    public DateTime RecordedAt { get; set; }

    public User? User { get; set; }
}
