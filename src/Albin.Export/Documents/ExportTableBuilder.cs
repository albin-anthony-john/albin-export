namespace Albin.Export.Documents;

/// <summary>
/// Fluent, reflection-free builder for an <see cref="ExportTable"/>.
/// </summary>
/// <typeparam name="T">The item type the columns read from.</typeparam>
public sealed class ExportTableBuilder<T>
{
    private readonly IEnumerable<T>? _items;
    private readonly IAsyncEnumerable<T>? _asyncItems;
    private readonly List<ExportColumn> _columns = [];
    private readonly List<Func<T, object?>> _accessors = [];
    private string? _name;
    private bool _freezeHeader = true;
    private bool _autoFilter = true;
    private IReadOnlyList<object?>? _totals;

    internal ExportTableBuilder(IEnumerable<T> items, string? name)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _name = name;
    }

    internal ExportTableBuilder(IAsyncEnumerable<T> items, string? name)
    {
        ArgumentNullException.ThrowIfNull(items);
        _asyncItems = items;
        _name = name;
    }

    /// <summary>
    /// Adds a column.
    /// </summary>
    /// <typeparam name="TValue">The value type of the column.</typeparam>
    /// <param name="header">The header text.</param>
    /// <param name="value">The accessor invoked once per row.</param>
    /// <param name="format">An optional .NET format string, for example <c>"N2"</c>.</param>
    /// <param name="alignment">The horizontal alignment. Defaults to a type based choice.</param>
    /// <param name="width">An optional width hint in characters.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public ExportTableBuilder<T> Column<TValue>(
        string header,
        Func<T, TValue> value,
        string? format = null,
        ExportAlignment alignment = ExportAlignment.Auto,
        double? width = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        _columns.Add(new ExportColumn(header)
        {
            Format = format,
            Alignment = alignment,
            Width = width,
            ValueType = typeof(TValue),
        });
        _accessors.Add(item => value(item));
        return this;
    }

    /// <summary>
    /// Sets the table name (worksheet name / caption).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportTableBuilder<T> Named(string? name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Configures Excel-specific table behaviour.
    /// </summary>
    /// <param name="freezeHeader">Whether the header row is frozen.</param>
    /// <param name="autoFilter">Whether an auto-filter is applied.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportTableBuilder<T> WithHeaderBehaviour(bool freezeHeader = true, bool autoFilter = true)
    {
        _freezeHeader = freezeHeader;
        _autoFilter = autoFilter;
        return this;
    }

    /// <summary>
    /// Adds a totals/footer row aligned with the declared columns.
    /// </summary>
    /// <param name="values">The footer cell values.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportTableBuilder<T> WithTotals(params object?[] values)
    {
        _totals = values;
        return this;
    }

    /// <summary>
    /// Builds the table.
    /// </summary>
    /// <returns>The configured <see cref="ExportTable"/>.</returns>
    /// <exception cref="InvalidOperationException">No column has been declared.</exception>
    public ExportTable Build()
    {
        if (_columns.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one column must be declared before building the table.");
        }

        var accessors = _accessors.ToArray();
        var rows = _items is not null
            ? ExportRowSource.FromItems(_items, accessors)
            : ExportRowSource.FromAsyncItems(_asyncItems!, accessors);

        return new ExportTable([.. _columns], rows, _name)
        {
            FreezeHeader = _freezeHeader,
            AutoFilter = _autoFilter,
            TotalsRow = _totals,
        };
    }

    /// <summary>
    /// Builds the table and wraps it in a single-section document.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <returns>A document containing only this table.</returns>
    public ExportDocument BuildDocument(string? title = null)
        => new() { Title = title ?? _name, Sections = [Build()] };
}
