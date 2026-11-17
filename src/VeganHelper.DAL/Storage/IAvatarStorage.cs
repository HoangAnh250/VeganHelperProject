namespace VeganHelper.DAL.Storage;

public interface IAvatarStorage
{
    bool OwnsAvatar(string? url) => false;
    Task DeleteAvatarAsync(string url, CancellationToken ct) => Task.CompletedTask;
    Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken cancellationToken);
}
