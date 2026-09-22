namespace Albin.Export.Documents;

/// <summary>
/// A tabular section: a set of columns plus a streaming row source.
/// </summary>
/// <remarks>
/// <see cref="ExportTable"/> is the unit every tabular exporter understands. Build one explicitly with
/// <see cref="Create{T}(IEnumerable{T}, string?)"/> for the fastest, reflection-free path, or use
/// <see cref="FromItems{T}(IEnumerable{T}, string?)"/> to derive the columns from the item's public properties.
/// </remarks>
public sealed class ExportTable : ExportSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportTable"/> class.
    /// </summary>
    /// <param name="columns">The column definitions. Must contain at least one column.</param>
    /// <param name="rows">The row source.</param>
    /// <param name="name">An optional table name, used as the worksheet name or table caption.</param>
    /// <exception cref="ArgumentNullException"><paramref name="columns"/> or <paramref name="rows"/> is <see langword="null"/>.</exception>
    public ExportTable(IReadOnlyList<ExportColumn> columns, IExportRowSource rows, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        Columns = columns;
        Rows = rows;
        Name = name;
    }

    /// <summary>Gets the optional table name (worksheet name / caption).</summary>
    public string? Name { get; }

    /// <summary>Gets the column definitions.</summary>
    public IReadOnlyList<ExportColumn> Columns { get; }

    /// <summary>Gets the row source.</summary>
    public IExportRowSource Rows { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the header row should be frozen where supported (Excel).
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool FreezeHeader { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an auto-filter should be applied where supported (Excel).
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool AutoFilter { get; init; } = true;

    /// <summary>
    /// Gets or sets an optional footer row, for example totals. Values are aligned with <see cref="Columns"/>.
    /// </summary>
    public IReadOnlyList<object?>? TotalsRow { get; init; }

    /// <summary>
    /// Starts a strongly typed, reflection-free table builder.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="name">An optional table name.</param>
    /// <returns>A builder used to declare the columns.</returns>
    /// <example>
    /// <code>
    /// var table = ExportTable.Create(invoices, "Invoices")
    ///     .Column("Number", x =&gt; x.Number)
    ///     .Column("Date", x =&gt; x.Date, format: "yyyy-MM-dd")
    ///     .Column("Total", x =&gt; x.Total, format: "N2")
    ///     .Build();
    /// </code>
    /// </example>
    public static ExportTableBuilder<T> Create<T>(IEnumerable<T> items, string? name = null)
        => new(items, name);

    /// <summary>
    /// Starts a strongly typed builder over an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="name">An optional table name.</param>
    /// <returns>A builder used to declare the columns.</returns>
    public static ExportTableBuilder<T> Create<T>(IAsyncEnumerable<T> items, string? name = null)
        => new(items, name);

    /// <summary>
    /// Creates a table whose columns are derived from the public readable instance properties of
    /// <typeparamref name="T"/>, in declaration order.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="name">An optional table name. Defaults to the type name.</param>
    /// <returns>The created table.</returns>
    /// <remarks>
    /// Property accessors are compiled once per type and cached, so this is fast after the first call.
    /// Declare columns explicitly with <see cref="Create{T}(IEnumerable{T}, string?)"/> when you need
    /// trim/AOT friendliness or custom headers.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static ExportTable FromItems<T>(IEnumerable<T> items, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var descriptor = ExportTypeDescriptor<T>.Instance;
        return new ExportTable(
            descriptor.Columns,
            ExportRowSource.FromItems(items, descriptor.Accessors),
            name ?? descriptor.TypeName);
    }

    /// <summary>
    /// Creates a table whose columns are derived from the public readable instance properties of
    /// <typeparamref name="T"/>, reading from an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="name">An optional table name. Defaults to the type name.</param>
    /// <returns>The created table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static ExportTable FromItems<T>(IAsyncEnumerable<T> items, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var descriptor = ExportTypeDescriptor<T>.Instance;
        return new ExportTable(
            descriptor.Columns,
            ExportRowSource.FromAsyncItems(items, descriptor.Accessors),
            name ?? descriptor.TypeName);
    }

    /// <summary>
    /// Creates a table from explicit headers and pre-materialized rows.
    /// </summary>
    /// <param name="headers">The column headers.</param>
    /// <param name="rows">The rows; each array holds one row of cells.</param>
    /// <param name="name">An optional table name.</param>
    /// <returns>The created table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="rows"/> is <see langword="null"/>.</exception>
    public static ExportTable FromCells(IEnumerable<string> headers, IEnumerable<object?[]> rows, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        var columns = headers.Select(h => new ExportColumn(h)).ToArray();
        return new ExportTable(columns, ExportRowSource.FromCells(rows), name);
    }

    /// <summary>
    /// Creates an empty table with the supplied headers.
    /// </summary>
    /// <param name="headers">The column headers.</param>
    /// <param name="name">An optional table name.</param>
    /// <returns>The created table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public static ExportTable Empty(IEnumerable<string> headers, string? name = null)
        => FromCells(headers, [], name);
}
