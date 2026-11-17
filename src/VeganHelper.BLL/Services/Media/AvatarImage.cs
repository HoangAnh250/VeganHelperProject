using SkiaSharp;

namespace VeganHelper.BLL.Services.Media;

internal static class AvatarImage
{
    public static bool IsValid(byte[] content, string contentType)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width is < 1 or > 2048 || codec.Info.Height is < 1 or > 2048)
            return false;

        var expected = contentType == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        if (codec.EncodedFormat != expected) return false;

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888);
        using var bitmap = new SKBitmap(info);
        return bitmap.GetPixels() != IntPtr.Zero && codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success;
    }
}
