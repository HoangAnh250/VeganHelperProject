using VeganHelper.DAL.Media;

namespace VeganHelper.UnitTests;

public sealed class VideoDurationReaderTests
{
    [Theory]
    [InlineData(0.25)]
    [InlineData(59.999)]
    [InlineData(60)]
    [InlineData(60.001)]
    [InlineData(120)]
    public void ReadsActualContainerDurationAcrossSupportedFormats(double seconds)
    {
        (byte[] Bytes, string Mime)[] samples =
        [
            (VideoTestData.Mp4(seconds), "video/mp4"),
            (VideoTestData.Mp4(seconds, version1: true, extendedSize: true), "video/quicktime"),
            (VideoTestData.Webm(seconds), "video/webm"),
            (VideoTestData.Webm(seconds, unknownSegmentSize: true), "video/webm")
        ];
        foreach (var sample in samples)
        {
            using var stream = new MemoryStream(sample.Bytes);
            stream.Position = 3;
            Assert.Equal(seconds, VideoDurationReader.ReadSeconds(stream, sample.Mime)!.Value, 3);
            Assert.Equal(3, stream.Position);
        }
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/webm")]
    public void TruncatedMetadataAndInvalidContainersHaveNoDuration(string mime)
    {
        var bytes = mime == "video/mp4" ? VideoTestData.Mp4(60) : VideoTestData.Webm(60);
        using var truncated = new MemoryStream(bytes[..^2]);
        Assert.Null(VideoDurationReader.ReadSeconds(truncated, mime));
        using var invalid = new MemoryStream(new byte[24]);
        Assert.Null(VideoDurationReader.ReadSeconds(invalid, mime));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(0)]
    public void RejectsInvalidWebmDurations(double seconds)
    {
        using var stream = new MemoryStream(VideoTestData.Webm(seconds));
        Assert.Null(VideoDurationReader.ReadSeconds(stream, "video/webm"));
    }
}
