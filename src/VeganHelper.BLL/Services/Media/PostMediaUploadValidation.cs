using Microsoft.AspNetCore.Http;
using System.Text;

namespace VeganHelper.BLL.Services.Media;

internal static class PostMediaUploadValidation
{
    public static async Task ValidateAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0) throw new ArgumentException("Media file must not be empty.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validType = extension switch
        {
            ".jpg" or ".jpeg" => mime == "image/jpeg",
            ".png" => mime == "image/png",
            ".webp" => mime == "image/webp",
            ".mp4" => mime == "video/mp4",
            ".mov" => mime == "video/quicktime",
            ".webm" => mime == "video/webm",
            _ => false
        };
        if (!validType) throw new ArgumentException("Unsupported media extension or content type.");
        var maximum = mime.StartsWith("image/") ? 5L * 1024 * 1024 : 200L * 1024 * 1024;
        if (file.Length > maximum) throw new ArgumentException("Images must be at most 5 MB; videos at most 200 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var length = await stream.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false, cancellationToken: ct);
        var validSignature = extension switch
        {
            ".jpg" or ".jpeg" => length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            ".png" => length >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => length >= 12 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            ".mp4" => length >= 8 && Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
            ".mov" => length >= 8 && Encoding.ASCII.GetString(header, 4, 4) is "ftyp" or "moov" or "mdat" or "wide",
            ".webm" => length >= 4 && header[0] == 0x1a && header[1] == 0x45 && header[2] == 0xdf && header[3] == 0xa3,
            _ => false
        };
        if (!validSignature) throw new ArgumentException("Media content does not match its declared format.");
    }
}
