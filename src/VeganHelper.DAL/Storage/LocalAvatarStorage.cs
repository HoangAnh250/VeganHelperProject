namespace VeganHelper.DAL.Storage;

public sealed class LocalAvatarStorage(string rootPath) : IAvatarStorage
{
    private readonly string _rootPath = Path.GetFullPath(rootPath);

    public async Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken cancellationToken)
    {
        var extension = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        Directory.CreateDirectory(_rootPath);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_rootPath, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return $"/uploads/avatars/{fileName}";
    }
}
