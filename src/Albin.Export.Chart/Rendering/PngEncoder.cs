using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace Albin.Export.Chart.Rendering;

/// <summary>
/// A minimal PNG writer for 8 bit RGBA images.
/// </summary>
/// <remarks>
/// Uses <see cref="ZLibStream"/> from the base class library for the deflate stream, so no image or
/// compression dependency is needed. Scanline filters None/Sub/Up are evaluated per row and the
/// cheapest is used, which keeps chart PNGs small.
/// </remarks>
internal static class PngEncoder
{
    private const int BytesPerPixel = 4;

    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// Writes an RGBA pixel buffer as a PNG image.
    /// </summary>
    /// <param name="destination">The stream that receives the PNG bytes.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="rgba">The pixel buffer, row major, four bytes per pixel.</param>
    public static void Write(Stream destination, int width, int height, ReadOnlySpan<byte> rgba)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var expected = (long)width * height * BytesPerPixel;
        if (rgba.Length < expected)
        {
            throw new ArgumentException("Pixel buffer is smaller than width * height * 4.", nameof(rgba));
        }

        destination.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..8], height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: truecolour with alpha
        header[10] = 0; // deflate
        header[11] = 0; // adaptive filtering
        header[12] = 0; // no interlace
        WriteChunk(destination, "IHDR", header);

        var compressed = Compress(width, height, rgba);
        WriteChunk(destination, "IDAT", compressed);
        WriteChunk(destination, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static byte[] Compress(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var stride = width * BytesPerPixel;
        var output = new MemoryStream(Math.Max(stride * height / 4, 1024));

        var pool = ArrayPool<byte>.Shared;
        var candidate = pool.Rent(stride + 1);
        var best = pool.Rent(stride + 1);
        try
        {
            using (var deflate = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var y = 0; y < height; y++)
                {
                    var row = rgba.Slice(y * stride, stride);
                    var previous = y == 0 ? [] : rgba.Slice((y - 1) * stride, stride);
                    var length = SelectFilter(row, previous, stride, candidate, best);
                    deflate.Write(best.AsSpan(0, length));
                }
            }

            return output.ToArray();
        }
        finally
        {
            pool.Return(candidate);
            pool.Return(best);
            output.Dispose();
        }
    }

    private static int SelectFilter(
        ReadOnlySpan<byte> row,
        ReadOnlySpan<byte> previous,
        int stride,
        byte[] candidate,
        byte[] best)
    {
        var bestScore = long.MaxValue;
        var maxFilter = previous.IsEmpty ? 1 : 2;

        for (var filter = 0; filter <= maxFilter; filter++)
        {
            candidate[0] = (byte)filter;
            long score = 0;
            for (var i = 0; i < stride; i++)
            {
                var left = i >= BytesPerPixel ? row[i - BytesPerPixel] : (byte)0;
                var up = previous.IsEmpty ? (byte)0 : previous[i];
                var value = filter switch
                {
                    1 => (byte)(row[i] - left),
                    2 => (byte)(row[i] - up),
                    _ => row[i],
                };

                candidate[i + 1] = value;
                score += value < 128 ? value : 256 - value;
            }

            if (score < bestScore)
            {
                bestScore = score;
                candidate.AsSpan(0, stride + 1).CopyTo(best);
            }
        }

        return stride + 1;
    }

    private static void WriteChunk(Stream destination, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        destination.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        destination.Write(typeBytes);
        destination.Write(data);

        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        destination.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in first)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in second)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256u; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
