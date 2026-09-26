namespace VeganHelper.DAL.Models;

public sealed class PostStep
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
}
