using Albin.Export.Abstractions;

namespace Albin.Export.Html;

/// <summary>
/// How charts are represented in HTML output.
/// </summary>
public enum HtmlChartMode
{
    /// <summary>
    /// Render the chart server-side as inline SVG. The document stays self-contained and prints
    /// correctly with no scripts. This is the default.
    /// </summary>
    InlineSvg = 0,

    /// <summary>
    /// Emit an Apache ECharts container carrying the option object in a <c>data-</c> attribute, for
    /// the browser to render interactively. Set <see cref="HtmlExportOptions.EChartsScriptUrl"/> to
    /// also emit the loader.
    /// </summary>
    EChartsClient = 1,

    /// <summary>Leave charts out of the document.</summary>
    None = 2,
}

/// <summary>
/// Options for <see cref="HtmlExporter"/>.
/// </summary>
public sealed class HtmlExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether only the body fragment is produced (no
    /// <c>&lt;html&gt;</c>, <c>&lt;head&gt;</c> or stylesheet). Useful when embedding the output in a
    /// Razor layout. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Fragment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the built-in stylesheet is emitted.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeDefaultStyles { get; set; } = true;

    /// <summary>
    /// Gets or sets additional CSS appended after the built-in stylesheet.
    /// </summary>
    /// <remarks>
    /// This is the one place where markup is emitted verbatim, so it must come from your application,
    /// never from user input.
    /// </remarks>
    public string? AdditionalCss { get; set; }

    /// <summary>
    /// Gets or sets the <c>lang</c> attribute of the document. Defaults to <c>en</c>.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets how charts are rendered. Defaults to <see cref="HtmlChartMode.InlineSvg"/>.
    /// </summary>
    public HtmlChartMode ChartMode { get; set; } = HtmlChartMode.InlineSvg;

    /// <summary>
    /// Gets or sets the URL of the Apache ECharts library used when
    /// <see cref="ChartMode"/> is <see cref="HtmlChartMode.EChartsClient"/>. When
    /// <see langword="null"/> no script tag is emitted and the page is expected to load ECharts itself.
    /// </summary>
    public string? EChartsScriptUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether zebra striping is applied to table rows.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool StripedTables { get; set; } = true;
}
