namespace VeganHelper.DAL.Storage;

public interface IAvatarStorage
{
    Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken cancellationToken);
}
