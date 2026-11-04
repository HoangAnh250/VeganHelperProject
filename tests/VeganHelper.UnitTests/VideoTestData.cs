using System.Buffers.Binary;
using System.Text;

namespace VeganHelper.UnitTests;

// Small container metadata fixtures; these tests do not pretend to decode video or call Gemini.
internal static class VideoTestData
{
    public static byte[] Mp4(double seconds, bool version1 = false, bool extendedSize = false)
    {
        var metadata = new byte[version1 ? 32 : 20];
        metadata[0] = version1 ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(metadata.AsSpan(version1 ? 20 : 12, 4), 1000);
        if (version1) BinaryPrimitives.WriteUInt64BigEndian(metadata.AsSpan(24, 8), (ulong)(seconds * 1000));
        else BinaryPrimitives.WriteUInt32BigEndian(metadata.AsSpan(16, 4), (uint)(seconds * 1000));
        return [.. Box("ftyp", "isom0000"u8.ToArray()), .. Box("moov", Box("mvhd", metadata), extendedSize)];
    }

    public static byte[] Webm(double seconds, bool unknownSegmentSize = false)
    {
        var duration = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(duration, BitConverter.DoubleToInt64Bits(seconds * 1000));
        var info = Element([0x15, 0x49, 0xa9, 0x66], [.. Element([0x2a, 0xd7, 0xb1], [0x0f, 0x42, 0x40]), .. Element([0x44, 0x89], duration)]);
        byte[] segment = unknownSegmentSize ? [0x18, 0x53, 0x80, 0x67, 0xff, .. info] : Element([0x18, 0x53, 0x80, 0x67], info);
        return [.. Element([0x1a, 0x45, 0xdf, 0xa3], []), .. segment];
    }

    private static byte[] Element(byte[] id, byte[] data) => [.. id, (byte)(0x80 | data.Length), .. data];

    private static byte[] Box(string type, byte[] data, bool extended = false)
    {
        var header = new byte[extended ? 16 : 8];
        BinaryPrimitives.WriteUInt32BigEndian(header, extended ? 1U : (uint)(8 + data.Length));
        Encoding.ASCII.GetBytes(type).CopyTo(header, 4);
        if (extended) BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), (ulong)(16 + data.Length));
        return [.. header, .. data];
    }
}
