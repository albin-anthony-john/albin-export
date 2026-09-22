using System.Globalization;
using Albin.Export.Chart.Rendering;
using Albin.Export.Documents;

namespace Albin.Export.Chart;

/// <summary>
/// Renders a <see cref="ChartSpec"/> to a PNG image entirely in managed code.
/// </summary>
/// <remarks>
/// There is no browser, headless runtime or native imaging library involved: the chart is rasterized
/// with a small built-in rasterizer and an embedded bitmap font, then encoded with
/// <see cref="System.IO.Compression.ZLibStream"/>. The same bytes are produced on Windows, Linux and
/// in containers. For publication quality output prefer <see cref="ChartSvgRenderer"/>.
/// </remarks>
public static class ChartPngRenderer
{
    private static readonly ChartColor Background = ChartColor.White;
    private static readonly ChartColor Text = new(36, 41, 47);
    private static readonly ChartColor Muted = new(87, 96, 106);
    private static readonly ChartColor Grid = new(208, 215, 222);

    /// <summary>
    /// Renders the chart and writes the PNG bytes to a stream.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <param name="destination">The stream that receives the PNG.</param>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    public static void Render(ChartSpec spec, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(destination);

        var canvas = Rasterize(spec);
        PngEncoder.Write(destination, canvas.Width, canvas.Height, canvas.Pixels);
    }

    /// <summary>
    /// Renders the chart to a PNG byte array.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <returns>The encoded PNG bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> is <see langword="null"/>.</exception>
    public static byte[] Render(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using var buffer = new MemoryStream(16 * 1024);
        Render(spec, buffer);
        return buffer.ToArray();
    }

    private static ChartCanvas Rasterize(ChartSpec spec)
    {
        var layout = new ChartLayout(spec);
        var canvas = new ChartCanvas(spec.Width, spec.Height, Background);

        DrawTitles(spec, canvas);

        if (spec.Type == ChartType.Pie)
        {
            DrawPie(layout, canvas);
        }
        else
        {
            DrawAxes(layout, canvas);
            switch (spec.Type)
            {
                case ChartType.Bar:
                    DrawBars(layout, canvas);
                    break;
                case ChartType.Area:
                    DrawLines(layout, canvas, fill: true);
                    break;
                default:
                    DrawLines(layout, canvas, fill: false);
                    break;
            }
        }

        DrawLegend(layout, canvas);
        return canvas;
    }

    private static void DrawTitles(ChartSpec spec, ChartCanvas canvas)
    {
        var y = 14d;
        if (!string.IsNullOrWhiteSpace(spec.Title))
        {
            canvas.DrawText(spec.Width / 2d, y, spec.Title, Text, scale: 2, TextAnchor.Middle);
            y += 24d;
        }

        if (!string.IsNullOrWhiteSpace(spec.Subtitle))
        {
            canvas.DrawText(spec.Width / 2d, y, spec.Subtitle, Muted, scale: 1, TextAnchor.Middle);
        }
    }

    private static void DrawAxes(ChartLayout layout, ChartCanvas canvas)
    {
        var spec = layout.Spec;

        foreach (var tick in layout.Ticks)
        {
            var y = layout.ValueY(tick);
            canvas.FillRect(layout.Left, y - 0.5d, layout.PlotWidth, 1d, Grid);
            canvas.DrawText(layout.Left - 8d, y - 3d, layout.FormatTick(tick), Muted, 1, TextAnchor.End);
        }

        if (layout.YMin < 0d && layout.YMax > 0d)
        {
            canvas.FillRect(layout.Left, layout.ValueY(0d) - 0.5d, layout.PlotWidth, 1d, new ChartColor(140, 149, 159));
        }

        for (var i = 0; i < spec.Categories.Count; i++)
        {
            if (i % layout.LabelStride != 0)
            {
                continue;
            }

            canvas.DrawText(layout.CategoryCenter(i), layout.Bottom + 8d, spec.Categories[i], Muted, 1, TextAnchor.Middle);
        }

        if (!string.IsNullOrWhiteSpace(spec.XAxisLabel))
        {
            canvas.DrawText((layout.Left + layout.Right) / 2d, layout.Bottom + 22d, spec.XAxisLabel, Muted, 1, TextAnchor.Middle);
        }

        if (!string.IsNullOrWhiteSpace(spec.YAxisLabel))
        {
            canvas.DrawTextVertical(16d, (layout.Top + layout.Bottom) / 2d, spec.YAxisLabel, Muted);
        }
    }

    private static void DrawBars(ChartLayout layout, ChartCanvas canvas)
    {
        var spec = layout.Spec;
        var baseline = layout.ValueY(Math.Clamp(0d, layout.YMin, layout.YMax));

        for (var s = 0; s < spec.Series.Count; s++)
        {
            var color = ChartColor.Parse(spec.ColorFor(s), Text);
            var values = spec.Series[s].Values;
            for (var c = 0; c < spec.Categories.Count && c < values.Count; c++)
            {
                if (values[c] is not { } value || double.IsNaN(value))
                {
                    continue;
                }

                var (x, width) = layout.Bar(s, c);
                var y = layout.ValueY(value);
                var top = Math.Min(y, baseline);
                var height = Math.Max(Math.Abs(baseline - y), 1d);
                canvas.FillRect(x, top, Math.Max(width - 1d, 1d), height, color);

                if (spec.ShowValueLabels)
                {
                    canvas.DrawText(x + (width / 2d), top - 10d, layout.FormatValue(value), Muted, 1, TextAnchor.Middle);
                }
            }
        }
    }

    private static void DrawLines(ChartLayout layout, ChartCanvas canvas, bool fill)
    {
        var spec = layout.Spec;
        var baseline = layout.ValueY(Math.Clamp(0d, layout.YMin, layout.YMax));

        for (var s = 0; s < spec.Series.Count; s++)
        {
            var color = ChartColor.Parse(spec.ColorFor(s), Text);
            var values = spec.Series[s].Values;
            var points = new List<(double X, double Y)>(spec.Categories.Count);

            for (var c = 0; c < spec.Categories.Count && c < values.Count; c++)
            {
                if (values[c] is not { } value || double.IsNaN(value))
                {
                    continue;
                }

                points.Add((layout.CategoryCenter(c), layout.ValueY(value)));
            }

            if (points.Count == 0)
            {
                continue;
            }

            if (fill)
            {
                var translucent = color.WithAlpha(0.18d);
                for (var i = 1; i < points.Count; i++)
                {
                    FillTrapezoid(canvas, points[i - 1], points[i], baseline, translucent);
                }
            }

            canvas.DrawPolyline(points.ToArray(), 2d, color);
            foreach (var point in points)
            {
                canvas.FillCircle(point.X, point.Y, 3d, color);
            }

            if (spec.ShowValueLabels)
            {
                var labelled = 0;
                for (var c = 0; c < values.Count && labelled < points.Count; c++)
                {
                    if (values[c] is not { } value)
                    {
                        continue;
                    }

                    var point = points[labelled++];
                    canvas.DrawText(point.X, point.Y - 14d, layout.FormatValue(value), Muted, 1, TextAnchor.Middle);
                }
            }
        }
    }

    private static void FillTrapezoid(
        ChartCanvas canvas,
        (double X, double Y) left,
        (double X, double Y) right,
        double baseline,
        ChartColor color)
    {
        // Approximate the area under the segment with one-pixel wide columns.
        var startX = Math.Min(left.X, right.X);
        var endX = Math.Max(left.X, right.X);
        var span = endX - startX;
        if (span <= 0)
        {
            return;
        }

        for (var x = startX; x < endX; x += 1d)
        {
            var t = (x - left.X) / (right.X - left.X);
            var y = left.Y + ((right.Y - left.Y) * t);
            var top = Math.Min(y, baseline);
            var height = Math.Abs(baseline - y);
            if (height > 0)
            {
                canvas.FillRect(x, top, Math.Min(1d, endX - x), height, color);
            }
        }
    }

    private static void DrawPie(ChartLayout layout, ChartCanvas canvas)
    {
        var spec = layout.Spec;
        var values = spec.Series[0].Values;
        var total = 0d;
        foreach (var value in values)
        {
            if (value is { } v && v > 0d && !double.IsNaN(v))
            {
                total += v;
            }
        }

        var cx = (layout.Left + layout.Right) / 2d;
        var cy = (layout.Top + layout.Bottom) / 2d;
        var radius = Math.Max(Math.Min(layout.PlotWidth, layout.PlotHeight) / 2d * 0.9d, 1d);

        if (total <= 0d)
        {
            canvas.FillCircle(cx, cy, radius, new ChartColor(234, 238, 242));
            return;
        }

        var startAngle = -Math.PI / 2d;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } value || value <= 0d || double.IsNaN(value))
            {
                continue;
            }

            var sweep = value / total * Math.PI * 2d;
            canvas.FillPieSlice(cx, cy, radius, startAngle, sweep, ChartColor.Parse(spec.ColorFor(i), Text));

            if (spec.ShowValueLabels && sweep > 0.18d)
            {
                var mid = startAngle + (sweep / 2d);
                var lx = cx + (radius * 0.62d * Math.Cos(mid));
                var ly = cy + (radius * 0.62d * Math.Sin(mid));
                var percent = (value / total * 100d).ToString("F0", CultureInfo.InvariantCulture) + "%";
                canvas.DrawText(lx, ly - 3d, percent, ChartColor.White, 1, TextAnchor.Middle);
            }

            startAngle += sweep;
        }
    }

    private static void DrawLegend(ChartLayout layout, ChartCanvas canvas)
    {
        var spec = layout.Spec;
        if (!spec.ShowLegend)
        {
            return;
        }

        var labels = spec.Type == ChartType.Pie
            ? spec.Categories
            : [.. spec.Series.Select(static s => s.Name)];

        if (labels.Count == 0)
        {
            return;
        }

        var x = layout.Left;
        var y = layout.LegendY + 4d;
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i] ?? string.Empty;
            var width = 14d + BitmapFont.Measure(label, 1) + 14d;
            if (x + width > spec.Width - 12d && x > layout.Left)
            {
                break;
            }

            canvas.FillRect(x, y, 10d, 10d, ChartColor.Parse(spec.ColorFor(i), Text));
            canvas.DrawText(x + 14d, y + 2d, label, Muted, 1, TextAnchor.Start);
            x += width;
        }
    }
}
