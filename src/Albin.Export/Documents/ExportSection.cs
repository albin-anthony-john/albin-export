namespace Albin.Export.Documents;

/// <summary>
/// Base class for the content blocks of an <see cref="ExportDocument"/>.
/// </summary>
/// <remarks>
/// Exporters pattern-match on the concrete section type and silently skip section types they
/// do not understand, which makes the model safe to extend.
/// </remarks>
public abstract class ExportSection
{
}

/// <summary>
/// A heading, rendered as <c>h1</c>-<c>h6</c> in HTML and as the matching heading style elsewhere.
/// </summary>
public sealed class ExportHeading : ExportSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportHeading"/> class.
    /// </summary>
    /// <param name="text">The heading text.</param>
    /// <param name="level">The heading level, clamped to the range 1-6.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public ExportHeading(string text, int level = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Level = Math.Clamp(level, 1, 6);
    }

    /// <summary>Gets the heading text.</summary>
    public string Text { get; }

    /// <summary>Gets the heading level between 1 and 6.</summary>
    public int Level { get; }
}

/// <summary>
/// A block of text.
/// </summary>
public sealed class ExportParagraph : ExportSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportParagraph"/> class.
    /// </summary>
    /// <param name="text">The paragraph text. Line breaks are preserved where the format allows it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public ExportParagraph(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the paragraph text.</summary>
    public string Text { get; }

    /// <summary>Gets or sets a value indicating whether the text is emphasized.</summary>
    public bool Bold { get; init; }

    /// <summary>Gets or sets a value indicating whether the text is rendered in a subdued style.</summary>
    public bool Muted { get; init; }
}

/// <summary>
/// A raster image embedded in the document.
/// </summary>
public sealed class ExportImage : ExportSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportImage"/> class.
    /// </summary>
    /// <param name="data">The encoded image bytes (PNG or JPEG).</param>
    /// <param name="contentType">The image media type, for example <c>image/png</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="contentType"/> is <see langword="null"/> or whitespace.</exception>
    public ExportImage(ReadOnlyMemory<byte> data, string contentType = "image/png")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        Data = data;
        ContentType = contentType;
    }

    /// <summary>Gets the encoded image bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>Gets the image media type.</summary>
    public string ContentType { get; }

    /// <summary>Gets or sets the display width in points. When <see langword="null"/> the natural size is used.</summary>
    public double? Width { get; init; }

    /// <summary>Gets or sets the display height in points. When <see langword="null"/> the aspect ratio is preserved.</summary>
    public double? Height { get; init; }

    /// <summary>Gets or sets alternative text used for accessibility in HTML and Word output.</summary>
    public string? AltText { get; init; }
}

/// <summary>
/// Forces a page break in paginated formats (PDF, Word). Ignored by the other formats.
/// </summary>
public sealed class ExportPageBreak : ExportSection
{
    /// <summary>Gets the shared page break instance.</summary>
    public static ExportPageBreak Instance { get; } = new();
}

/// <summary>
/// A chart, rendered as an image in paginated formats and as an Apache ECharts container in HTML.
/// </summary>
public sealed class ExportChart : ExportSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportChart"/> class.
    /// </summary>
    /// <param name="chart">The chart definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chart"/> is <see langword="null"/>.</exception>
    public ExportChart(ChartSpec chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        Chart = chart;
    }

    /// <summary>Gets the chart definition.</summary>
    public ChartSpec Chart { get; }
}
