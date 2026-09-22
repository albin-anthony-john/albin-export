namespace Albin.Export.Documents;

/// <summary>
/// The kind of chart to render.
/// </summary>
public enum ChartType
{
    /// <summary>Vertical bars, one group per category.</summary>
    Bar = 0,

    /// <summary>Connected points, one per category.</summary>
    Line = 1,

    /// <summary>A filled line chart.</summary>
    Area = 2,

    /// <summary>A pie chart built from the first series.</summary>
    Pie = 3,
}

/// <summary>
/// One data series of a <see cref="ChartSpec"/>.
/// </summary>
public sealed class ChartSeries
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartSeries"/> class.
    /// </summary>
    /// <param name="name">The series name shown in the legend.</param>
    /// <param name="values">The values, aligned with <see cref="ChartSpec.Categories"/>. <see langword="null"/> entries are gaps.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public ChartSeries(string name, IReadOnlyList<double?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        Name = name;
        Values = values;
    }

    /// <summary>Gets the series name.</summary>
    public string Name { get; }

    /// <summary>Gets the series values.</summary>
    public IReadOnlyList<double?> Values { get; }

    /// <summary>
    /// Gets or sets an explicit CSS colour (for example <c>#4e79a7</c>).
    /// When <see langword="null"/> a colour from the palette is used.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Creates a series from a sequence of non-nullable values.
    /// </summary>
    /// <param name="name">The series name.</param>
    /// <param name="values">The values.</param>
    /// <returns>The created series.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public static ChartSeries From(string name, IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ChartSeries(name, [.. values.Select(v => (double?)v)]);
    }
}

/// <summary>
/// A renderer-agnostic chart definition.
/// </summary>
/// <remarks>
/// The same specification feeds every chart output: an Apache ECharts option object
/// (<c>ExportFormat.ECharts</c>), server-side SVG (<c>ExportFormat.Svg</c>) and PNG
/// (<c>ExportFormat.Png</c>). Nothing here depends on a browser runtime.
/// </remarks>
public sealed class ChartSpec
{
    /// <summary>
    /// The default categorical colour palette (colour-blind friendly).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultPalette =
    [
        "#4e79a7", "#f28e2b", "#e15759", "#76b7b2", "#59a14f",
        "#edc948", "#b07aa1", "#ff9da7", "#9c755f", "#bab0ac",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartSpec"/> class.
    /// </summary>
    /// <param name="type">The chart type.</param>
    /// <param name="categories">The category (x axis) labels.</param>
    /// <param name="series">The data series.</param>
    /// <exception cref="ArgumentNullException"><paramref name="categories"/> or <paramref name="series"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="series"/> is empty.</exception>
    public ChartSpec(ChartType type, IReadOnlyList<string> categories, IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count == 0)
        {
            throw new ArgumentException("A chart needs at least one series.", nameof(series));
        }

        Type = type;
        Categories = categories;
        Series = series;
    }

    /// <summary>Gets the chart type.</summary>
    public ChartType Type { get; }

    /// <summary>Gets the category (x axis) labels.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>Gets the data series.</summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>Gets or sets the chart title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets or sets the chart subtitle.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Gets or sets the x axis label.</summary>
    public string? XAxisLabel { get; init; }

    /// <summary>Gets or sets the y axis label.</summary>
    public string? YAxisLabel { get; init; }

    /// <summary>Gets or sets the canvas width in pixels. Defaults to 900.</summary>
    public int Width { get; init; } = 900;

    /// <summary>Gets or sets the canvas height in pixels. Defaults to 480.</summary>
    public int Height { get; init; } = 480;

    /// <summary>Gets or sets a value indicating whether the legend is shown. Defaults to <see langword="true"/>.</summary>
    public bool ShowLegend { get; init; } = true;

    /// <summary>Gets or sets a value indicating whether value labels are drawn on data points. Defaults to <see langword="false"/>.</summary>
    public bool ShowValueLabels { get; init; }

    /// <summary>Gets or sets the palette used for series without an explicit colour.</summary>
    public IReadOnlyList<string> Palette { get; init; } = DefaultPalette;

    /// <summary>
    /// Gets the colour assigned to a series.
    /// </summary>
    /// <param name="index">The zero based series index.</param>
    /// <returns>The explicit series colour, or the palette entry for the index.</returns>
    public string ColorFor(int index)
    {
        if (index >= 0 && index < Series.Count && Series[index].Color is { Length: > 0 } color)
        {
            return color;
        }

        var palette = Palette.Count > 0 ? Palette : DefaultPalette;
        return palette[((index % palette.Count) + palette.Count) % palette.Count];
    }
}
