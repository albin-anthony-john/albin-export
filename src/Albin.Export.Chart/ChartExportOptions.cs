using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Chart;

/// <summary>
/// Options for the chart exporters (<c>Svg</c>, <c>Png</c> and <c>ECharts</c>).
/// </summary>
public sealed class ChartExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the chart to render. When <see langword="null"/> the exporter uses the first
    /// <see cref="ExportChart"/> section of the document, and failing that derives a chart from the
    /// first table (first column as categories, numeric columns as series).
    /// </summary>
    public ChartSpec? Chart { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ECharts JSON is pretty printed. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IndentJson { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of categories read when a chart is derived from a table.
    /// Defaults to 200, which keeps an accidental million-row export from producing an unusable chart.
    /// </summary>
    public int MaxDerivedCategories { get; set; } = 200;
}
