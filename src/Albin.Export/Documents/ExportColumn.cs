namespace Albin.Export.Documents;

/// <summary>
/// Horizontal alignment of a table column.
/// </summary>
public enum ExportAlignment
{
    /// <summary>Let the exporter decide (numbers right, everything else left).</summary>
    Auto = 0,

    /// <summary>Align to the left.</summary>
    Left = 1,

    /// <summary>Center the content.</summary>
    Center = 2,

    /// <summary>Align to the right.</summary>
    Right = 3,
}

/// <summary>
/// Describes a single column of an <see cref="ExportTable"/>.
/// </summary>
public sealed class ExportColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportColumn"/> class.
    /// </summary>
    /// <param name="header">The header text shown for this column.</param>
    /// <exception cref="ArgumentException"><paramref name="header"/> is <see langword="null"/> or whitespace.</exception>
    public ExportColumn(string header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        Header = header;
    }

    /// <summary>
    /// Gets the header text shown for this column.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Gets or sets an optional .NET format string (for example <c>"N2"</c> or <c>"yyyy-MM-dd"</c>)
    /// applied to values of this column.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Gets or sets the horizontal alignment of the column. Defaults to <see cref="ExportAlignment.Auto"/>.
    /// </summary>
    public ExportAlignment Alignment { get; init; } = ExportAlignment.Auto;

    /// <summary>
    /// Gets or sets an optional column width hint, expressed in characters.
    /// Exporters map it to their own unit or ignore it.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>
    /// Gets or sets the CLR type of the values in this column. Defaults to <see cref="object"/>.
    /// Exporters use it to write strongly typed cells (Excel) or to pick a default alignment.
    /// </summary>
    public Type ValueType { get; init; } = typeof(object);

    /// <summary>
    /// Gets a value indicating whether <see cref="ValueType"/> is a numeric type.
    /// </summary>
    public bool IsNumeric => ExportValueTypes.IsNumeric(ValueType);

    /// <summary>
    /// Resolves the effective alignment, replacing <see cref="ExportAlignment.Auto"/> with a
    /// type based default.
    /// </summary>
    /// <returns><see cref="ExportAlignment.Right"/> for numeric columns, otherwise <see cref="ExportAlignment.Left"/>.</returns>
    public ExportAlignment ResolveAlignment() => Alignment != ExportAlignment.Auto
        ? Alignment
        : IsNumeric ? ExportAlignment.Right : ExportAlignment.Left;

    /// <inheritdoc />
    public override string ToString() => Header;
}
