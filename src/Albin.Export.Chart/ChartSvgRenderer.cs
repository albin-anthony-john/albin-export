using System.Globalization;
using System.Text;
using Albin.Export.Chart.Rendering;
using Albin.Export.Documents;

namespace Albin.Export.Chart;

/// <summary>
/// Renders a <see cref="ChartSpec"/> to a standalone SVG document on the server.
/// </summary>
/// <remarks>
/// The output is plain SVG 1.1 with no scripts, no external references and no embedded fonts, so it
/// is safe to inline into HTML, convert to PDF or store as a file. All text is XML-escaped.
/// </remarks>
public static class ChartSvgRenderer
{
    private const string FontFamily = "Segoe UI, Helvetica, Arial, sans-serif";
    private const string AxisColor = "#d0d7de";
    private const string TextColor = "#24292f";
    private const string MutedColor = "#57606a";

    /// <summary>
    /// Renders the chart to an SVG document.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <returns>The SVG markup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> is <see langword="null"/>.</exception>
    public static string Render(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var builder = new StringBuilder(4096);
        Render(spec, builder);
        return builder.ToString();
    }

    /// <summary>
    /// Renders the chart into an existing <see cref="StringBuilder"/>, which avoids an extra copy when
    /// the SVG is inlined into a larger document.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <param name="output">The builder that receives the markup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    public static void Render(ChartSpec spec, StringBuilder output)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(output);

        var layout = new ChartLayout(spec);

        output.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(spec.Width)
            .Append("\" height=\"").Append(spec.Height)
            .Append("\" viewBox=\"0 0 ").Append(spec.Width).Append(' ').Append(spec.Height)
            .Append("\" role=\"img\"");

        if (!string.IsNullOrWhiteSpace(spec.Title))
        {
            output.Append(" aria-label=\"").Append(Escape(spec.Title)).Append('"');
        }

        output.Append('>');
        output.Append("<rect x=\"0\" y=\"0\" width=\"").Append(spec.Width)
            .Append("\" height=\"").Append(spec.Height).Append("\" fill=\"#ffffff\"/>");
        output.Append("<g font-family=\"").Append(FontFamily).Append("\" font-size=\"11\" fill=\"")
            .Append(TextColor).Append("\">");

        WriteTitles(spec, output);

        if (spec.Type == ChartType.Pie)
        {
            WritePie(layout, output);
        }
        else
        {
            WriteAxes(layout, output);
            switch (spec.Type)
            {
                case ChartType.Bar:
                    WriteBars(layout, output);
                    break;
                case ChartType.Area:
                    WriteLines(layout, output, fill: true);
                    break;
                default:
                    WriteLines(layout, output, fill: false);
                    break;
            }
        }

        WriteLegend(layout, output);
        output.Append("</g></svg>");
    }

    private static void WriteTitles(ChartSpec spec, StringBuilder output)
    {
        var y = 26d;
        if (!string.IsNullOrWhiteSpace(spec.Title))
        {
            output.Append("<text x=\"").Append(N(spec.Width / 2d)).Append("\" y=\"").Append(N(y))
                .Append("\" text-anchor=\"middle\" font-size=\"16\" font-weight=\"600\">")
                .Append(Escape(spec.Title)).Append("</text>");
            y += 20d;
        }

        if (!string.IsNullOrWhiteSpace(spec.Subtitle))
        {
            output.Append("<text x=\"").Append(N(spec.Width / 2d)).Append("\" y=\"").Append(N(y))
                .Append("\" text-anchor=\"middle\" fill=\"").Append(MutedColor).Append("\">")
                .Append(Escape(spec.Subtitle)).Append("</text>");
        }
    }

    private static void WriteAxes(ChartLayout layout, StringBuilder output)
    {
        var spec = layout.Spec;

        foreach (var tick in layout.Ticks)
        {
            var y = layout.ValueY(tick);
            output.Append("<line x1=\"").Append(N(layout.Left)).Append("\" y1=\"").Append(N(y))
                .Append("\" x2=\"").Append(N(layout.Right)).Append("\" y2=\"").Append(N(y))
                .Append("\" stroke=\"").Append(AxisColor).Append("\" stroke-width=\"1\"/>");
            output.Append("<text x=\"").Append(N(layout.Left - 8d)).Append("\" y=\"").Append(N(y + 4d))
                .Append("\" text-anchor=\"end\" fill=\"").Append(MutedColor).Append("\">")
                .Append(Escape(layout.FormatTick(tick))).Append("</text>");
        }

        // Zero line gets a stronger stroke when the scale spans it.
        if (layout.YMin < 0d && layout.YMax > 0d)
        {
            var zero = layout.ValueY(0d);
            output.Append("<line x1=\"").Append(N(layout.Left)).Append("\" y1=\"").Append(N(zero))
                .Append("\" x2=\"").Append(N(layout.Right)).Append("\" y2=\"").Append(N(zero))
                .Append("\" stroke=\"#8c959f\" stroke-width=\"1\"/>");
        }

        for (var i = 0; i < spec.Categories.Count; i++)
        {
            if (i % layout.LabelStride != 0)
            {
                continue;
            }

            output.Append("<text x=\"").Append(N(layout.CategoryCenter(i)))
                .Append("\" y=\"").Append(N(layout.Bottom + 16d))
                .Append("\" text-anchor=\"middle\" fill=\"").Append(MutedColor).Append("\">")
                .Append(Escape(spec.Categories[i])).Append("</text>");
        }

        if (!string.IsNullOrWhiteSpace(spec.XAxisLabel))
        {
            output.Append("<text x=\"").Append(N((layout.Left + layout.Right) / 2d))
                .Append("\" y=\"").Append(N(layout.Bottom + 34d))
                .Append("\" text-anchor=\"middle\" fill=\"").Append(MutedColor).Append("\">")
                .Append(Escape(spec.XAxisLabel)).Append("</text>");
        }

        if (!string.IsNullOrWhiteSpace(spec.YAxisLabel))
        {
            var cy = (layout.Top + layout.Bottom) / 2d;
            output.Append("<text transform=\"translate(18 ").Append(N(cy)).Append(") rotate(-90)\"")
                .Append(" text-anchor=\"middle\" fill=\"").Append(MutedColor).Append("\">")
                .Append(Escape(spec.YAxisLabel)).Append("</text>");
        }
    }

    private static void WriteBars(ChartLayout layout, StringBuilder output)
    {
        var spec = layout.Spec;
        var baseline = layout.ValueY(Math.Clamp(0d, layout.YMin, layout.YMax));

        for (var s = 0; s < spec.Series.Count; s++)
        {
            var color = spec.ColorFor(s);
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

                output.Append("<rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(top))
                    .Append("\" width=\"").Append(N(Math.Max(width - 1d, 1d))).Append("\" height=\"").Append(N(height))
                    .Append("\" fill=\"").Append(Escape(color)).Append("\"/>");

                if (spec.ShowValueLabels)
                {
                    output.Append("<text x=\"").Append(N(x + (width / 2d))).Append("\" y=\"").Append(N(top - 4d))
                        .Append("\" text-anchor=\"middle\" font-size=\"10\" fill=\"").Append(MutedColor).Append("\">")
                        .Append(Escape(layout.FormatValue(value))).Append("</text>");
                }
            }
        }
    }

    private static void WriteLines(ChartLayout layout, StringBuilder output, bool fill)
    {
        var spec = layout.Spec;
        var baseline = layout.ValueY(Math.Clamp(0d, layout.YMin, layout.YMax));

        for (var s = 0; s < spec.Series.Count; s++)
        {
            var color = spec.ColorFor(s);
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

            if (fill && points.Count > 1)
            {
                output.Append("<path fill=\"").Append(Escape(color)).Append("\" fill-opacity=\"0.18\" d=\"M ")
                    .Append(N(points[0].X)).Append(' ').Append(N(baseline));
                foreach (var (x, y) in points)
                {
                    output.Append(" L ").Append(N(x)).Append(' ').Append(N(y));
                }

                output.Append(" L ").Append(N(points[^1].X)).Append(' ').Append(N(baseline)).Append(" Z\"/>");
            }

            output.Append("<polyline fill=\"none\" stroke=\"").Append(Escape(color))
                .Append("\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\" points=\"");
            for (var i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    output.Append(' ');
                }

                output.Append(N(points[i].X)).Append(',').Append(N(points[i].Y));
            }

            output.Append("\"/>");

            foreach (var (x, y) in points)
            {
                output.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
                    .Append("\" r=\"3\" fill=\"").Append(Escape(color)).Append("\"/>");
            }

            if (spec.ShowValueLabels)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    var value = values[i];
                    if (value is null)
                    {
                        continue;
                    }

                    output.Append("<text x=\"").Append(N(points[i].X)).Append("\" y=\"").Append(N(points[i].Y - 8d))
                        .Append("\" text-anchor=\"middle\" font-size=\"10\" fill=\"").Append(MutedColor).Append("\">")
                        .Append(Escape(layout.FormatValue(value.Value))).Append("</text>");
                }
            }
        }
    }

    private static void WritePie(ChartLayout layout, StringBuilder output)
    {
        var spec = layout.Spec;
        var values = spec.Series[0].Values;
        var total = 0d;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is { } v && v > 0d && !double.IsNaN(v))
            {
                total += v;
            }
        }

        var cx = (layout.Left + layout.Right) / 2d;
        var cy = (layout.Top + layout.Bottom) / 2d;
        var radius = Math.Max(Math.Min(layout.PlotWidth, layout.PlotHeight) / 2d * 0.9d, 1d);

        if (total <= 0d)
        {
            output.Append("<circle cx=\"").Append(N(cx)).Append("\" cy=\"").Append(N(cy))
                .Append("\" r=\"").Append(N(radius)).Append("\" fill=\"#eaeef2\"/>");
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
            var endAngle = startAngle + sweep;
            var color = spec.ColorFor(i);

            if (Math.Abs(sweep - (Math.PI * 2d)) < 1e-9)
            {
                output.Append("<circle cx=\"").Append(N(cx)).Append("\" cy=\"").Append(N(cy))
                    .Append("\" r=\"").Append(N(radius)).Append("\" fill=\"").Append(Escape(color)).Append("\"/>");
            }
            else
            {
                var (x0, y0) = (cx + (radius * Math.Cos(startAngle)), cy + (radius * Math.Sin(startAngle)));
                var (x1, y1) = (cx + (radius * Math.Cos(endAngle)), cy + (radius * Math.Sin(endAngle)));
                var largeArc = sweep > Math.PI ? 1 : 0;

                output.Append("<path fill=\"").Append(Escape(color)).Append("\" stroke=\"#ffffff\" stroke-width=\"1\" d=\"M ")
                    .Append(N(cx)).Append(' ').Append(N(cy))
                    .Append(" L ").Append(N(x0)).Append(' ').Append(N(y0))
                    .Append(" A ").Append(N(radius)).Append(' ').Append(N(radius)).Append(" 0 ")
                    .Append(largeArc).Append(" 1 ").Append(N(x1)).Append(' ').Append(N(y1))
                    .Append(" Z\"/>");
            }

            if (spec.ShowValueLabels && sweep > 0.18d)
            {
                var mid = startAngle + (sweep / 2d);
                var lx = cx + (radius * 0.62d * Math.Cos(mid));
                var ly = cy + (radius * 0.62d * Math.Sin(mid));
                var percent = value / total * 100d;
                output.Append("<text x=\"").Append(N(lx)).Append("\" y=\"").Append(N(ly + 4d))
                    .Append("\" text-anchor=\"middle\" font-size=\"11\" fill=\"#ffffff\" font-weight=\"600\">")
                    .Append(Escape(percent.ToString("F0", CultureInfo.InvariantCulture))).Append("%</text>");
            }

            startAngle = endAngle;
        }
    }

    private static void WriteLegend(ChartLayout layout, StringBuilder output)
    {
        var spec = layout.Spec;
        if (!spec.ShowLegend)
        {
            return;
        }

        var labels = spec.Type == ChartType.Pie
            ? spec.Categories
            : spec.Series.Select(static s => s.Name).ToArray();

        if (labels.Count == 0)
        {
            return;
        }

        var x = layout.Left;
        var y = layout.LegendY + 8d;
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i] ?? string.Empty;
            var width = 18d + (label.Length * 6.4d) + 14d;
            if (x + width > spec.Width - 12d && x > layout.Left)
            {
                break;
            }

            output.Append("<rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(y - 8d))
                .Append("\" width=\"10\" height=\"10\" rx=\"2\" fill=\"").Append(Escape(spec.ColorFor(i))).Append("\"/>");
            output.Append("<text x=\"").Append(N(x + 15d)).Append("\" y=\"").Append(N(y + 1d))
                .Append("\" fill=\"").Append(MutedColor).Append("\">").Append(Escape(label)).Append("</text>");
            x += width;
        }
    }

    private static string N(double value)
        => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}
