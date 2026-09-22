using System.Buffers.Binary;

namespace Albin.Export.Word;

/// <summary>
/// Reads the pixel dimensions of a PNG or JPEG image straight from its header.
/// </summary>
/// <remarks>
/// Word needs an explicit extent for every inline image. Parsing the few header bytes avoids pulling
/// in an imaging library just to learn how big a picture is.
/// </remarks>
internal static class ImageInfo
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Attempts to read the dimensions of an encoded image.
    /// </summary>
    public static bool TryGetSize(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length >= 24 && data[..8].SequenceEqual(PngSignature))
        {
            width = BinaryPrimitives.ReadInt32BigEndian(data[16..20]);
            height = BinaryPrimitives.ReadInt32BigEndian(data[20..24]);
            return width > 0 && height > 0;
        }

        return TryGetJpegSize(data, out width, out height);
    }

    private static bool TryGetJpegSize(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        var offset = 2;
        while (offset + 9 < data.Length)
        {
            if (data[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = data[offset + 1];
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                offset += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);

            // Start-of-frame markers carry the dimensions; SOF4, SOF8 and SOF12 are not frame headers.
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]);
                width = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 7)..]);
                return width > 0 && height > 0;
            }

            offset += 2 + length;
        }

        return false;
    }
}
