namespace Albin.Export.Documents;

/// <summary>
/// Supplies the rows of an <see cref="ExportTable"/> on demand.
/// </summary>
/// <remarks>
/// Implementations stream rows so that exporters never need the full dataset in memory.
/// A source may reuse a single cell buffer between rows; see <see cref="ExportRow"/>.
/// </remarks>
public interface IExportRowSource
{
    /// <summary>
    /// Gets the number of rows when it is known up front without enumerating, otherwise <see langword="null"/>.
    /// Exporters use it purely as a sizing hint.
    /// </summary>
    int? Count { get; }

    /// <summary>
    /// Streams the rows of the table.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>An asynchronous sequence of rows.</returns>
    IAsyncEnumerable<ExportRow> ReadAsync(CancellationToken cancellationToken = default);
}
