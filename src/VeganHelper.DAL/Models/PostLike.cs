namespace VeganHelper.DAL.Models;

public sealed class PostLike
{
    public long UserId { get; set; }
    public long PostId { get; set; }
    public DateTime CreatedAt { get; set; }
}
