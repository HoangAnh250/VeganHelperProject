namespace VeganHelper.DAL.Entities;

public sealed class UserFollow
{
    public long Id { get; set; }
    public long FollowerId { get; set; }
    public long FollowingId { get; set; }
    public bool NotifyOnNewPost { get; set; }
    public DateTime CreatedAt { get; set; }
}
