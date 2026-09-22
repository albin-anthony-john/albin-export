using System.Text.Json;
using Albin.Export.Documents;

namespace Albin.Export.Chart;

/// <summary>
/// Builds an Apache ECharts <c>option</c> object from a <see cref="ChartSpec"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the bridge between server-side data and browser-side rendering: the server produces only
/// the option object, and the browser (or a dashboard, or a saved <c>.json</c> file) feeds it to
/// <c>echarts.init(el).setOption(option)</c>. No browser runtime, JavaScript engine or headless
/// automation is involved on the server.
/// </para>
/// </remarks>
public static class EChartsOptionBuilder
{
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds the ECharts option object as a nested dictionary.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <returns>A dictionary that serializes to a valid ECharts option.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> is <see langword="null"/>.</exception>
    public static Dictionary<string, object?> Build(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var option = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["animation"] = false,
            ["color"] = Enumerable.Range(0, Math.Max(spec.Series.Count, spec.Categories.Count))
                .Select(spec.ColorFor)
                .ToArray(),
        };

        if (!string.IsNullOrWhiteSpace(spec.Title) || !string.IsNullOrWhiteSpace(spec.Subtitle))
        {
            option["title"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["text"] = spec.Title,
                ["subtext"] = spec.Subtitle,
                ["left"] = "center",
            };
        }

        option["tooltip"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["trigger"] = spec.Type == ChartType.Pie ? "item" : "axis",
        };

        if (spec.ShowLegend)
        {
            option["legend"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["bottom"] = 0,
                ["data"] = spec.Type == ChartType.Pie
                    ? spec.Categories.ToArray()
                    : spec.Series.Select(static s => s.Name).ToArray(),
            };
        }

        if (spec.Type == ChartType.Pie)
        {
            option["series"] = new[] { BuildPieSeries(spec) };
            return option;
        }

        option["grid"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["left"] = 16,
            ["right"] = 24,
            ["bottom"] = spec.ShowLegend ? 40 : 16,
            ["top"] = string.IsNullOrWhiteSpace(spec.Title) ? 24 : 64,
            ["containLabel"] = true,
        };

        option["xAxis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "category",
            ["data"] = spec.Categories.ToArray(),
            ["name"] = spec.XAxisLabel,
            ["nameLocation"] = string.IsNullOrWhiteSpace(spec.XAxisLabel) ? null : "middle",
            ["nameGap"] = string.IsNullOrWhiteSpace(spec.XAxisLabel) ? null : 28,
            ["boundaryGap"] = spec.Type == ChartType.Bar,
        };

        option["yAxis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "value",
            ["name"] = spec.YAxisLabel,
            ["nameLocation"] = string.IsNullOrWhiteSpace(spec.YAxisLabel) ? null : "middle",
            ["nameGap"] = string.IsNullOrWhiteSpace(spec.YAxisLabel) ? null : 44,
        };

        option["series"] = spec.Series.Select((series, index) => BuildCartesianSeries(spec, series, index)).ToArray();
        return option;
    }

    /// <summary>
    /// Serializes the ECharts option object to JSON.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <param name="indented">Whether the JSON is pretty printed. Defaults to compact output.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> is <see langword="null"/>.</exception>
    public static string ToJson(ChartSpec spec, bool indented = false)
        => JsonSerializer.Serialize(Build(spec), indented ? IndentedJson : CompactJson);

    /// <summary>
    /// Serializes the ECharts option object using caller supplied serializer options.
    /// </summary>
    /// <param name="spec">The chart definition.</param>
    /// <param name="serializerOptions">The serializer options to use.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spec"/> or <paramref name="serializerOptions"/> is <see langword="null"/>.</exception>
    public static string ToJson(ChartSpec spec, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        return JsonSerializer.Serialize(Build(spec), serializerOptions);
    }

    private static Dictionary<string, object?> BuildPieSeries(ChartSpec spec)
    {
        var values = spec.Series[0].Values;
        var data = new List<Dictionary<string, object?>>(spec.Categories.Count);
        for (var i = 0; i < spec.Categories.Count; i++)
        {
            data.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = spec.Categories[i],
                ["value"] = i < values.Count ? values[i] : null,
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = spec.Series[0].Name,
            ["type"] = "pie",
            ["radius"] = "62%",
            ["data"] = data,
            ["label"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["show"] = spec.ShowValueLabels },
        };
    }

    private static Dictionary<string, object?> BuildCartesianSeries(ChartSpec spec, ChartSeries series, int index)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = series.Name,
            ["type"] = spec.Type == ChartType.Bar ? "bar" : "line",
            ["data"] = series.Values.ToArray(),
            ["itemStyle"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["color"] = spec.ColorFor(index) },
        };

        if (spec.Type == ChartType.Area)
        {
            result["areaStyle"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["opacity"] = 0.18 };
            result["smooth"] = true;
        }

        if (spec.ShowValueLabels)
        {
            result["label"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["show"] = true,
                ["position"] = "top",
            };
        }

        return result;
    }
}
