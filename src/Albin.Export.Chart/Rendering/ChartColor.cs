using System.Globalization;

namespace Albin.Export.Chart.Rendering;

/// <summary>
/// A straight-alpha 8 bit RGBA colour used by the PNG rasterizer.
/// </summary>
internal readonly record struct ChartColor(byte R, byte G, byte B, byte A = 255)
{
    public static ChartColor White => new(255, 255, 255);

    public static ChartColor Black => new(0, 0, 0);

    public static ChartColor Transparent => new(0, 0, 0, 0);

    /// <summary>
    /// Parses a CSS style hex colour (<c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>).
    /// Unrecognized input falls back to <paramref name="fallback"/>.
    /// </summary>
    public static ChartColor Parse(string? value, ChartColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var span = value.AsSpan().Trim();
        if (span.Length == 0 || span[0] != '#')
        {
            return fallback;
        }

        span = span[1..];
        return span.Length switch
        {
            3 when TryHex(span[0], out var r) && TryHex(span[1], out var g) && TryHex(span[2], out var b)
                => new ChartColor((byte)(r * 17), (byte)(g * 17), (byte)(b * 17)),
            6 when TryByte(span[..2], out var r6) && TryByte(span[2..4], out var g6) && TryByte(span[4..6], out var b6)
                => new ChartColor(r6, g6, b6),
            8 when TryByte(span[..2], out var r8) && TryByte(span[2..4], out var g8) && TryByte(span[4..6], out var b8)
                     && TryByte(span[6..8], out var a8)
                => new ChartColor(r8, g8, b8, a8),
            _ => fallback,
        };
    }

    public ChartColor WithAlpha(double alpha)
        => new(R, G, B, (byte)Math.Clamp(alpha * 255d, 0d, 255d));

    private static bool TryHex(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
        return value >= 0;
    }

    private static bool TryByte(ReadOnlySpan<char> span, out byte value)
        => byte.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
