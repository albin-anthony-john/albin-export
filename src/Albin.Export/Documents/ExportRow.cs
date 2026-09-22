namespace Albin.Export.Documents;

/// <summary>
/// A single row of table cell values.
/// </summary>
/// <remarks>
/// <para>
/// A row is a thin view over a cell buffer. Row sources are allowed to reuse the same buffer for
/// every row they yield, which keeps large exports allocation free. Consumers must therefore read
/// the values they need before advancing the enumerator and must never store an <see cref="ExportRow"/>
/// for later use. Call <see cref="ToArray"/> when a durable copy is required.
/// </para>
/// </remarks>
public readonly struct ExportRow
{
    private readonly object?[] _cells;
    private readonly int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRow"/> struct.
    /// </summary>
    /// <param name="cells">The cell buffer. The row exposes the whole buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cells"/> is <see langword="null"/>.</exception>
    public ExportRow(object?[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _cells = cells;
        _length = cells.Length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRow"/> struct over the first
    /// <paramref name="length"/> entries of a (possibly larger) buffer.
    /// </summary>
    /// <param name="cells">The cell buffer.</param>
    /// <param name="length">The number of cells that belong to this row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cells"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or larger than the buffer.</exception>
    public ExportRow(object?[] cells, int length)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, cells.Length);
        _cells = cells;
        _length = length;
    }

    /// <summary>
    /// Gets the number of cells in the row.
    /// </summary>
    public int FieldCount => _length;

    /// <summary>
    /// Gets the raw value of a cell.
    /// </summary>
    /// <param name="index">The zero based cell index.</param>
    /// <returns>The cell value, possibly <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the row.</exception>
    public object? this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);
            return _cells[index];
        }
    }

    /// <summary>
    /// Gets the cells of this row as a span.
    /// </summary>
    /// <returns>A span over the live cell buffer; it is only valid until the enumerator advances.</returns>
    public ReadOnlySpan<object?> AsSpan() => _cells is null ? default : _cells.AsSpan(0, _length);

    /// <summary>
    /// Copies the row into a new array.
    /// </summary>
    /// <returns>A detached copy of the cell values.</returns>
    public object?[] ToArray() => AsSpan().ToArray();
}
