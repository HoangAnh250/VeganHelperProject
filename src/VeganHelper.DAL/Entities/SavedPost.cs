namespace VeganHelper.DAL.Entities;

public sealed class SavedPost
{
    public long UserId { get; set; }
    public long PostId { get; set; }
    public DateTime SavedAt { get; set; }
}
