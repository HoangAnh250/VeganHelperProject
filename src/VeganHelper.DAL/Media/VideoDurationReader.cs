using System.Buffers.Binary;

namespace VeganHelper.DAL.Media;

/// <summary>Reads container duration metadata without decoding or buffering the video payload.</summary>
public static class VideoDurationReader
{
    private const uint Mp4MovieHeader = 0x6d766864; // mvhd
    private const uint Mp4Movie = 0x6d6f6f76; // moov
    private const ulong WebmSegment = 0x18538067;
    private const ulong WebmInfo = 0x1549A966;
    private const ulong WebmDuration = 0x4489;
    private const ulong WebmTimecodeScale = 0x2AD7B1;

    public static double? ReadSeconds(Stream stream, string mimeType)
    {
        if (!stream.CanSeek || !stream.CanRead)
            return null;
        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            return mimeType.ToLowerInvariant() switch
            {
                "video/mp4" or "video/quicktime" or "video/mov" => ReadMp4Seconds(stream),
                "video/webm" => ReadWebmSeconds(stream),
                _ => null
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static double? ReadMp4Seconds(Stream stream)
    {
        var end = stream.Length;
        var header = new byte[16];
        while (stream.Position <= end - 8)
        {
            var boxStart = stream.Position;
            ReadExactly(stream, header.AsSpan(0, 8));
            var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            var type = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
            long headerLength = 8;
            ulong boxSize = size;
            if (size == 1)
            {
                ReadExactly(stream, header.AsSpan(8, 8));
                boxSize = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8, 8));
                headerLength = 16;
            }
            else if (size == 0)
                boxSize = checked((ulong)(end - boxStart));

            if (boxSize < (ulong)headerLength || boxSize > (ulong)(end - boxStart))
                return null;
            var boxEnd = checked(boxStart + (long)boxSize);
            if (type == Mp4Movie)
            {
                while (stream.Position <= boxEnd - 8)
                {
                    var childStart = stream.Position;
                    ReadExactly(stream, header.AsSpan(0, 8));
                    var childSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
                    var childType = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
                    long childHeaderLength = 8;
                    ulong fullChildSize = childSize;
                    if (childSize == 1)
                    {
                        ReadExactly(stream, header.AsSpan(8, 8));
                        fullChildSize = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8, 8));
                        childHeaderLength = 16;
                    }
                    else if (childSize == 0)
                        fullChildSize = checked((ulong)(boxEnd - childStart));
                    if (fullChildSize < (ulong)childHeaderLength || fullChildSize > (ulong)(boxEnd - childStart))
                        return null;
                    var childEnd = checked(childStart + (long)fullChildSize);
                    if (childType == Mp4MovieHeader)
                    {
                        var bodyLength = childEnd - stream.Position;
                        if (bodyLength < 20)
                            return null;
                        var metadata = new byte[32];
                        ReadExactly(stream, metadata.AsSpan(0, Math.Min(metadata.Length, (int)bodyLength)));
                        var version = metadata[0];
                        if (version == 0)
                        {
                            var timescale = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(12, 4));
                            var duration = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(16, 4));
                            return timescale == 0 || duration == uint.MaxValue ? null : (double)duration / timescale;
                        }
                        if (version == 1 && bodyLength >= 32)
                        {
                            var timescale = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(20, 4));
                            var duration = BinaryPrimitives.ReadUInt64BigEndian(metadata.AsSpan(24, 8));
                            return timescale == 0 || duration == ulong.MaxValue ? null : (double)duration / timescale;
                        }
                        return null;
                    }
                    stream.Position = childEnd;
                }
                return null;
            }
            stream.Position = boxEnd;
        }
        return null;
    }

    private static double? ReadWebmSeconds(Stream stream)
    {
        var fileEnd = (ulong)stream.Length;
        while (TryReadEbmlHeader(stream, fileEnd, out var element))
        {
            var end = element.UnknownSize ? fileEnd : element.DataStart + element.Size;
            if (element.Id == WebmSegment)
                return ReadWebmSegmentInfo(stream, end);
            stream.Position = checked((long)end);
        }
        return null;
    }

    private static double? ReadWebmSegmentInfo(Stream stream, ulong segmentEnd)
    {
        const ulong defaultTimecodeScale = 1_000_000;
        var scale = defaultTimecodeScale;
        double? duration = null;
        while (TryReadEbmlHeader(stream, segmentEnd, out var element))
        {
            var end = element.UnknownSize ? segmentEnd : element.DataStart + element.Size;
            if (end > segmentEnd)
                return null;
            if (element.Id == WebmInfo)
            {
                var numericBytes = new byte[8];
                while (TryReadEbmlHeader(stream, end, out var child))
                {
                    var childEnd = child.UnknownSize ? end : child.DataStart + child.Size;
                    if (childEnd > end)
                        return null;
                    if (child.Id == WebmTimecodeScale && child.Size is > 0 and <= 8)
                    {
                        Array.Clear(numericBytes);
                        ReadExactly(stream, numericBytes.AsSpan(8 - (int)child.Size, (int)child.Size));
                        scale = BinaryPrimitives.ReadUInt64BigEndian(numericBytes);
                    }
                    else if (child.Id == WebmDuration && child.Size is 4 or 8)
                    {
                        Array.Clear(numericBytes);
                        ReadExactly(stream, numericBytes.AsSpan(8 - (int)child.Size, (int)child.Size));
                        var bits = BinaryPrimitives.ReadUInt64BigEndian(numericBytes);
                        duration = child.Size == 4
                            ? BitConverter.Int32BitsToSingle(unchecked((int)(uint)bits))
                            : BitConverter.Int64BitsToDouble(unchecked((long)bits));
                    }
                    stream.Position = checked((long)childEnd);
                }
                if (duration is double value)
                {
                    var seconds = value * scale / 1_000_000_000d;
                    return double.IsFinite(seconds) && seconds > 0 ? seconds : null;
                }
                return null;
            }
            // WebM places Info before the media clusters; an unknown-size cluster means metadata is absent.
            if (element.UnknownSize)
                return null;
            stream.Position = checked((long)end);
        }
        return null;
    }

    private static bool TryReadEbmlHeader(Stream stream, ulong parentEnd, out EbmlElement element)
    {
        element = default;
        if ((ulong)stream.Position >= parentEnd)
            return false;
        var first = stream.ReadByte();
        if (first < 0)
            return false;
        var idLength = VintLength((byte)first);
        if (idLength is < 1 or > 4)
            return false;
        ulong id = (byte)first;
        for (var i = 1; i < idLength; i++)
        {
            var next = stream.ReadByte();
            if (next < 0) return false;
            id = (id << 8) | (byte)next;
        }

        var sizeFirst = stream.ReadByte();
        if (sizeFirst < 0)
            return false;
        var sizeLength = VintLength((byte)sizeFirst);
        if (sizeLength is < 1 or > 8)
            return false;
        var marker = (byte)(0x80 >> (sizeLength - 1));
        ulong size = (ulong)((byte)sizeFirst & (marker - 1));
        for (var i = 1; i < sizeLength; i++)
        {
            var next = stream.ReadByte();
            if (next < 0) return false;
            size = (size << 8) | (byte)next;
        }
        var unknown = size == ((1UL << (7 * sizeLength)) - 1);
        if ((ulong)stream.Position > parentEnd || (!unknown && size > parentEnd - (ulong)stream.Position))
            return false;
        element = new EbmlElement(id, (ulong)stream.Position, size, unknown);
        return true;
    }

    private static int VintLength(byte first)
    {
        for (var mask = 0x80; mask != 0; mask >>= 1)
            if ((first & mask) != 0)
                return 8 - System.Numerics.BitOperations.Log2((uint)mask);
        return 0;
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private readonly record struct EbmlElement(ulong Id, ulong DataStart, ulong Size, bool UnknownSize);
}
