using System.Globalization;
using Albin.Export.Documents;

namespace Albin.Export.Chart.Rendering;

/// <summary>
/// Computes the geometry shared by the SVG and PNG chart renderers so both produce the same layout.
/// </summary>
internal sealed class ChartLayout
{
    private const int TargetTickCount = 5;

    public ChartLayout(ChartSpec spec)
    {
        Spec = spec;

        var titleHeight = 0d;
        if (!string.IsNullOrWhiteSpace(spec.Title))
        {
            titleHeight += 30d;
        }

        if (!string.IsNullOrWhiteSpace(spec.Subtitle))
        {
            titleHeight += 20d;
        }

        var legendHeight = spec.ShowLegend && spec.Series.Count > 0 ? 26d : 0d;
        var axisLabelHeight = string.IsNullOrWhiteSpace(spec.XAxisLabel) ? 0d : 20d;

        Left = string.IsNullOrWhiteSpace(spec.YAxisLabel) ? 64d : 84d;
        Right = spec.Width - 24d;
        Top = 20d + titleHeight;
        Bottom = spec.Height - 34d - legendHeight - axisLabelHeight;
        LegendY = spec.Height - legendHeight + 4d;

        if (Right <= Left)
        {
            Right = Left + 1d;
        }

        if (Bottom <= Top)
        {
            Bottom = Top + 1d;
        }

        (YMin, YMax, Step) = NiceScale(spec);
        Ticks = BuildTicks(YMin, YMax, Step);
        TickDecimals = DecimalsFor(Step);
        LabelStride = ComputeLabelStride(spec, PlotWidth);
    }

    public ChartSpec Spec { get; }

    /// <summary>Left edge of the plot area.</summary>
    public double Left { get; }

    /// <summary>Right edge of the plot area.</summary>
    public double Right { get; }

    /// <summary>Top edge of the plot area.</summary>
    public double Top { get; }

    /// <summary>Bottom edge (the category axis) of the plot area.</summary>
    public double Bottom { get; }

    /// <summary>Baseline used to draw the legend.</summary>
    public double LegendY { get; }

    public double PlotWidth => Right - Left;

    public double PlotHeight => Bottom - Top;

    public double YMin { get; }

    public double YMax { get; }

    public double Step { get; }

    public IReadOnlyList<double> Ticks { get; }

    public int TickDecimals { get; }

    /// <summary>Draw every n-th category label so labels never overlap.</summary>
    public int LabelStride { get; }

    public int CategoryCount => Math.Max(Spec.Categories.Count, 1);

    public double Band => PlotWidth / CategoryCount;

    /// <summary>Horizontal centre of a category slot.</summary>
    public double CategoryCenter(int index) => Left + (Band * (index + 0.5));

    /// <summary>Vertical position of a value.</summary>
    public double ValueY(double value)
    {
        var span = YMax - YMin;
        if (span <= 0)
        {
            return Bottom;
        }

        var ratio = (value - YMin) / span;
        return Bottom - (Math.Clamp(ratio, 0d, 1d) * PlotHeight);
    }

    /// <summary>Rectangle of one bar in a grouped bar chart.</summary>
    public (double X, double Width) Bar(int seriesIndex, int categoryIndex)
    {
        var groupWidth = Band * 0.72;
        var barWidth = Math.Max(groupWidth / Math.Max(Spec.Series.Count, 1), 1d);
        var groupLeft = Left + (Band * categoryIndex) + ((Band - groupWidth) / 2d);
        return (groupLeft + (seriesIndex * barWidth), barWidth);
    }

    public string FormatTick(double value)
        => value.ToString("F" + TickDecimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public string FormatValue(double value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded == Math.Truncate(rounded)
            ? rounded.ToString("F0", CultureInfo.InvariantCulture)
            : rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static (double Min, double Max, double Step) NiceScale(ChartSpec spec)
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var series in spec.Series)
        {
            foreach (var value in series.Values)
            {
                if (value is not { } v || double.IsNaN(v) || double.IsInfinity(v))
                {
                    continue;
                }

                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }

        if (min > max)
        {
            return (0d, 1d, 0.25d);
        }

        // Bars and areas are read against zero, so always include it.
        if (spec.Type is ChartType.Bar or ChartType.Area)
        {
            min = Math.Min(min, 0d);
            max = Math.Max(max, 0d);
        }

        if (Math.Abs(max - min) < double.Epsilon)
        {
            var magnitude = Math.Max(Math.Abs(max), 1d);
            min -= magnitude * 0.1;
            max += magnitude * 0.1;
        }

        var rawStep = (max - min) / TargetTickCount;
        var exponent = Math.Floor(Math.Log10(rawStep));
        var power = Math.Pow(10d, exponent);
        var fraction = rawStep / power;
        var niceFraction = fraction switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 2.5d => 2.5d,
            <= 5d => 5d,
            _ => 10d,
        };

        var step = niceFraction * power;
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        return (niceMin, niceMax, step);
    }

    private static double[] BuildTicks(double min, double max, double step)
    {
        if (step <= 0)
        {
            return [min, max];
        }

        var count = (int)Math.Round((max - min) / step) + 1;
        count = Math.Clamp(count, 2, 21);
        var ticks = new double[count];
        for (var i = 0; i < count; i++)
        {
            ticks[i] = min + (step * i);
        }

        return ticks;
    }

    private static int DecimalsFor(double step)
    {
        if (step <= 0 || step >= 1)
        {
            return 0;
        }

        var decimals = (int)Math.Ceiling(-Math.Log10(step));
        return Math.Clamp(decimals, 0, 6);
    }

    private static int ComputeLabelStride(ChartSpec spec, double plotWidth)
    {
        if (spec.Categories.Count == 0)
        {
            return 1;
        }

        var longest = 1;
        foreach (var category in spec.Categories)
        {
            longest = Math.Max(longest, category?.Length ?? 0);
        }

        // ~6.2px per character at the 11px label size used by both renderers.
        var required = Math.Max(longest * 6.2, 12d) + 8d;
        var available = plotWidth / spec.Categories.Count;
        return available >= required ? 1 : (int)Math.Ceiling(required / Math.Max(available, 1d));
    }
}
