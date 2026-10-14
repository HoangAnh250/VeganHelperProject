namespace VeganHelper.DAL.Entities;

// Pending audience snapshot, captured atomically with first publication. Completed work is removed.
public sealed class PostFollowerNotification
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public long FollowId { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
