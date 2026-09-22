using Albin.Export.Abstractions;

namespace Albin.Export.Documents;

/// <summary>
/// A fully materialized, pre-formatted view of an <see cref="ExportTable"/>.
/// </summary>
/// <remarks>
/// <para>
/// PDF and Word are produced by libraries with synchronous object models: they need the whole table
/// before they can lay it out. Reading and formatting the rows up front — here, asynchronously and
/// with cancellation — keeps the blocking work out of the document writers and gives them the column
/// measurements they need to size columns sensibly.
/// </para>
/// <para>
/// This is the one place the framework trades memory for convenience. Streaming formats (CSV, JSON,
/// HTML, XML, Markdown) never use it.
/// </para>
/// </remarks>
public sealed class ExportTableSnapshot
{
    private ExportTableSnapshot(
        string? name,
        IReadOnlyList<ExportColumn> columns,
        string[] headers,
        ExportAlignment[] alignments,
        List<string[]> rows,
        string[]? totals,
        int[] maxTextLengths)
    {
        Name = name;
        Columns = columns;
        Headers = headers;
        Alignments = alignments;
        Rows = rows;
        Totals = totals;
        MaxTextLengths = maxTextLengths;
    }

    /// <summary>Gets the table name.</summary>
    public string? Name { get; }

    /// <summary>Gets the column definitions.</summary>
    public IReadOnlyList<ExportColumn> Columns { get; }

    /// <summary>Gets the header texts, one per column.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Gets the resolved alignment of each column.</summary>
    public IReadOnlyList<ExportAlignment> Alignments { get; }

    /// <summary>Gets the formatted rows.</summary>
    public IReadOnlyList<string[]> Rows { get; }

    /// <summary>Gets the formatted footer row, or <see langword="null"/> when the table has none.</summary>
    public IReadOnlyList<string>? Totals { get; }

    /// <summary>
    /// Gets the longest formatted text length seen per column, including the header.
    /// Document writers use it to distribute column widths.
    /// </summary>
    public IReadOnlyList<int> MaxTextLengths { get; }

    /// <summary>Gets the number of columns.</summary>
    public int ColumnCount => Columns.Count;

    /// <summary>
    /// Reads and formats a table.
    /// </summary>
    /// <param name="table">The table to materialize.</param>
    /// <param name="options">The options supplying culture, formats and null text.</param>
    /// <param name="cancellationToken">Token used to cancel row enumeration.</param>
    /// <returns>The materialized table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public static async ValueTask<ExportTableSnapshot> CreateAsync(
        ExportTable table,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        options ??= ExportOptions.Default;

        var columnCount = table.Columns.Count;
        var headers = new string[columnCount];
        var alignments = new ExportAlignment[columnCount];
        var lengths = new int[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            headers[i] = table.Columns[i].Header;
            alignments[i] = table.Columns[i].ResolveAlignment();
            lengths[i] = headers[i].Length;
        }

        var rows = new List<string[]>(table.Rows.Count ?? 32);
        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            var cells = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                var value = i < row.FieldCount ? row[i] : null;
                cells[i] = ExportValueFormatter.Format(value, table.Columns[i], options);
                if (cells[i].Length > lengths[i])
                {
                    lengths[i] = cells[i].Length;
                }
            }

            rows.Add(cells);
        }

        string[]? totals = null;
        if (table.TotalsRow is { Count: > 0 } source)
        {
            totals = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                var value = i < source.Count ? source[i] : null;
                totals[i] = ExportValueFormatter.Format(value, table.Columns[i], options);
                if (totals[i].Length > lengths[i])
                {
                    lengths[i] = totals[i].Length;
                }
            }
        }

        return new ExportTableSnapshot(table.Name, table.Columns, headers, alignments, rows, totals, lengths);
    }

    /// <summary>
    /// Distributes a total width across the columns, proportionally to their content length.
    /// </summary>
    /// <param name="totalWidth">The width available for the whole table, in any unit.</param>
    /// <param name="minimumWidth">The smallest width a column may receive, in the same unit.</param>
    /// <returns>One width per column, summing to <paramref name="totalWidth"/>.</returns>
    /// <remarks>
    /// Columns that declare <see cref="ExportColumn.Width"/> are honoured first (interpreted as
    /// characters at roughly six units per character); the remaining space is shared between the rest.
    /// </remarks>
    public double[] DistributeWidths(double totalWidth, double minimumWidth = 24d)
    {
        var count = ColumnCount;
        var widths = new double[count];
        if (count == 0)
        {
            return widths;
        }

        var remaining = totalWidth;
        var flexibleWeight = 0d;

        for (var i = 0; i < count; i++)
        {
            if (Columns[i].Width is { } declared and > 0)
            {
                widths[i] = Math.Min(declared * 6d, totalWidth);
                remaining -= widths[i];
            }
            else
            {
                flexibleWeight += Math.Clamp(MaxTextLengths[i], 4, 48);
            }
        }

        if (flexibleWeight <= 0)
        {
            return Normalize(widths, totalWidth);
        }

        remaining = Math.Max(remaining, minimumWidth * CountFlexible());
        for (var i = 0; i < count; i++)
        {
            if (widths[i] > 0)
            {
                continue;
            }

            var weight = Math.Clamp(MaxTextLengths[i], 4, 48) / flexibleWeight;
            widths[i] = Math.Max(remaining * weight, minimumWidth);
        }

        return Normalize(widths, totalWidth);

        int CountFlexible()
        {
            var flexible = 0;
            for (var i = 0; i < count; i++)
            {
                if (Columns[i].Width is null or <= 0)
                {
                    flexible++;
                }
            }

            return Math.Max(flexible, 1);
        }
    }

    private static double[] Normalize(double[] widths, double totalWidth)
    {
        var sum = 0d;
        foreach (var width in widths)
        {
            sum += width;
        }

        if (sum <= 0 || Math.Abs(sum - totalWidth) < 0.01)
        {
            return widths;
        }

        var scale = totalWidth / sum;
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] *= scale;
        }

        return widths;
    }
}
