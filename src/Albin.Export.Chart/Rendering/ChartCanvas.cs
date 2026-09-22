namespace Albin.Export.Chart.Rendering;

/// <summary>Horizontal anchoring used by <see cref="ChartCanvas.DrawText"/>.</summary>
internal enum TextAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>
/// A minimal software rasterizer over an 8 bit RGBA buffer.
/// </summary>
/// <remarks>
/// Only the primitives the chart renderer needs are implemented: filled rectangles, circles, pie
/// slices, thick line segments and bitmap text. Edges of the curved and diagonal primitives are
/// anti-aliased with 2x2 supersampling, which is enough for report quality output and costs nothing
/// in dependencies.
/// </remarks>
internal sealed class ChartCanvas
{
    private const int BytesPerPixel = 4;

    private readonly byte[] _pixels;

    public ChartCanvas(int width, int height, ChartColor background)
    {
        Width = Math.Max(width, 1);
        Height = Math.Max(height, 1);
        _pixels = new byte[Width * Height * BytesPerPixel];

        if (background.A != 0)
        {
            for (var i = 0; i < _pixels.Length; i += BytesPerPixel)
            {
                _pixels[i] = background.R;
                _pixels[i + 1] = background.G;
                _pixels[i + 2] = background.B;
                _pixels[i + 3] = background.A;
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Gets the raw RGBA pixels, row major, top row first.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    public void FillRect(double x, double y, double width, double height, ChartColor color)
    {
        if (width <= 0 || height <= 0 || color.A == 0)
        {
            return;
        }

        var x1 = x + width;
        var y1 = y + height;
        var startX = Math.Max((int)Math.Floor(x), 0);
        var startY = Math.Max((int)Math.Floor(y), 0);
        var endX = Math.Min((int)Math.Ceiling(x1), Width);
        var endY = Math.Min((int)Math.Ceiling(y1), Height);

        for (var py = startY; py < endY; py++)
        {
            var coverY = Math.Min(y1, py + 1d) - Math.Max(y, py);
            if (coverY <= 0)
            {
                continue;
            }

            for (var px = startX; px < endX; px++)
            {
                var coverX = Math.Min(x1, px + 1d) - Math.Max(x, px);
                if (coverX > 0)
                {
                    Blend(px, py, color, coverX * coverY);
                }
            }
        }
    }

    public void FillCircle(double cx, double cy, double radius, ChartColor color)
    {
        if (radius <= 0)
        {
            return;
        }

        var r2 = radius * radius;
        FillShape(cx - radius, cy - radius, cx + radius, cy + radius, color, (sx, sy) =>
        {
            var dx = sx - cx;
            var dy = sy - cy;
            return (dx * dx) + (dy * dy) <= r2;
        });
    }

    /// <summary>
    /// Fills a pie slice. Angles are in radians, measured clockwise from the positive x axis
    /// (the chart renderer starts at <c>-PI/2</c>, i.e. twelve o'clock).
    /// </summary>
    public void FillPieSlice(double cx, double cy, double radius, double startAngle, double sweep, ChartColor color)
    {
        if (radius <= 0 || sweep <= 0)
        {
            return;
        }

        if (sweep >= Math.PI * 2d)
        {
            FillCircle(cx, cy, radius, color);
            return;
        }

        var r2 = radius * radius;
        var normalizedStart = Normalize(startAngle);

        FillShape(cx - radius, cy - radius, cx + radius, cy + radius, color, (sx, sy) =>
        {
            var dx = sx - cx;
            var dy = sy - cy;
            if ((dx * dx) + (dy * dy) > r2)
            {
                return false;
            }

            var delta = Normalize(Math.Atan2(dy, dx) - normalizedStart);
            return delta <= sweep;
        });
    }

    public void DrawLine(double x0, double y0, double x1, double y1, double thickness, ChartColor color)
    {
        var half = Math.Max(thickness, 0.5d) / 2d;
        var dx = x1 - x0;
        var dy = y1 - y0;
        var lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared < 1e-9)
        {
            FillCircle(x0, y0, half, color);
            return;
        }

        var minX = Math.Min(x0, x1) - half - 1d;
        var maxX = Math.Max(x0, x1) + half + 1d;
        var minY = Math.Min(y0, y1) - half - 1d;
        var maxY = Math.Max(y0, y1) + half + 1d;
        var halfSquared = half * half;

        FillShape(minX, minY, maxX, maxY, color, (sx, sy) =>
        {
            var t = Math.Clamp((((sx - x0) * dx) + ((sy - y0) * dy)) / lengthSquared, 0d, 1d);
            var px = x0 + (t * dx) - sx;
            var py = y0 + (t * dy) - sy;
            return (px * px) + (py * py) <= halfSquared;
        });
    }

    public void DrawPolyline(ReadOnlySpan<(double X, double Y)> points, double thickness, ChartColor color)
    {
        for (var i = 1; i < points.Length; i++)
        {
            DrawLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, thickness, color);
        }
    }

    /// <summary>
    /// Draws text with the embedded bitmap font. <paramref name="y"/> is the top of the glyph box.
    /// </summary>
    public void DrawText(double x, double y, string? text, ChartColor color, int scale = 1, TextAnchor anchor = TextAnchor.Start)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        scale = Math.Max(scale, 1);
        var width = BitmapFont.Measure(text, scale);
        var originX = anchor switch
        {
            TextAnchor.Middle => x - (width / 2d),
            TextAnchor.End => x - width,
            _ => x,
        };

        var penX = (int)Math.Round(originX);
        var penY = (int)Math.Round(y);

        foreach (var c in text)
        {
            var glyph = BitmapFont.Glyph(c);
            for (var column = 0; column < BitmapFont.GlyphWidth; column++)
            {
                var bits = glyph[column];
                for (var row = 0; row < BitmapFont.GlyphHeight; row++)
                {
                    if ((bits & (1 << row)) == 0)
                    {
                        continue;
                    }

                    FillPixelBlock(penX + (column * scale), penY + (row * scale), scale, color);
                }
            }

            penX += BitmapFont.Advance * scale;
        }
    }

    /// <summary>
    /// Draws text rotated 90 degrees counter-clockwise, used for the y axis label.
    /// <paramref name="x"/> and <paramref name="y"/> are the centre of the text.
    /// </summary>
    public void DrawTextVertical(double x, double y, string? text, ChartColor color, int scale = 1)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        scale = Math.Max(scale, 1);
        var length = BitmapFont.Measure(text, scale);
        var penY = (int)Math.Round(y + (length / 2d));
        var penX = (int)Math.Round(x - (BitmapFont.GlyphHeight * scale / 2d));

        foreach (var c in text)
        {
            var glyph = BitmapFont.Glyph(c);
            for (var column = 0; column < BitmapFont.GlyphWidth; column++)
            {
                var bits = glyph[column];
                for (var row = 0; row < BitmapFont.GlyphHeight; row++)
                {
                    if ((bits & (1 << row)) == 0)
                    {
                        continue;
                    }

                    FillPixelBlock(penX + (row * scale), penY - (column * scale), scale, color);
                }
            }

            penY -= BitmapFont.Advance * scale;
        }
    }

    private void FillPixelBlock(int x, int y, int scale, ChartColor color)
    {
        for (var dy = 0; dy < scale; dy++)
        {
            for (var dx = 0; dx < scale; dx++)
            {
                Blend(x + dx, y + dy, color, 1d);
            }
        }
    }

    private void FillShape(
        double minX,
        double minY,
        double maxX,
        double maxY,
        ChartColor color,
        Func<double, double, bool> inside)
    {
        if (color.A == 0)
        {
            return;
        }

        var startX = Math.Max((int)Math.Floor(minX), 0);
        var startY = Math.Max((int)Math.Floor(minY), 0);
        var endX = Math.Min((int)Math.Ceiling(maxX) + 1, Width);
        var endY = Math.Min((int)Math.Ceiling(maxY) + 1, Height);

        for (var py = startY; py < endY; py++)
        {
            for (var px = startX; px < endX; px++)
            {
                var hits = 0;
                if (inside(px + 0.25d, py + 0.25d))
                {
                    hits++;
                }

                if (inside(px + 0.75d, py + 0.25d))
                {
                    hits++;
                }

                if (inside(px + 0.25d, py + 0.75d))
                {
                    hits++;
                }

                if (inside(px + 0.75d, py + 0.75d))
                {
                    hits++;
                }

                if (hits > 0)
                {
                    Blend(px, py, color, hits / 4d);
                }
            }
        }
    }

    private void Blend(int x, int y, ChartColor color, double coverage)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return;
        }

        var alpha = color.A / 255d * Math.Clamp(coverage, 0d, 1d);
        if (alpha <= 0d)
        {
            return;
        }

        var offset = ((y * Width) + x) * BytesPerPixel;
        var destinationAlpha = _pixels[offset + 3] / 255d;
        var outAlpha = alpha + (destinationAlpha * (1d - alpha));
        if (outAlpha <= 0d)
        {
            return;
        }

        _pixels[offset] = Mix(_pixels[offset], color.R, alpha, destinationAlpha, outAlpha);
        _pixels[offset + 1] = Mix(_pixels[offset + 1], color.G, alpha, destinationAlpha, outAlpha);
        _pixels[offset + 2] = Mix(_pixels[offset + 2], color.B, alpha, destinationAlpha, outAlpha);
        _pixels[offset + 3] = (byte)Math.Round(outAlpha * 255d);
    }

    private static byte Mix(byte destination, byte source, double alpha, double destinationAlpha, double outAlpha)
    {
        var value = ((source * alpha) + (destination * destinationAlpha * (1d - alpha))) / outAlpha;
        return (byte)Math.Clamp(Math.Round(value), 0d, 255d);
    }

    private static double Normalize(double angle)
    {
        var twoPi = Math.PI * 2d;
        angle %= twoPi;
        return angle < 0 ? angle + twoPi : angle;
    }
}
