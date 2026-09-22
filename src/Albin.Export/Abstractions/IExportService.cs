using Albin.Export.Documents;

namespace Albin.Export.Abstractions;

/// <summary>
/// The single entry point applications use to export data.
/// </summary>
/// <remarks>
/// Register it with <c>services.AddAlbinExport(export =&gt; export.AddPdf().AddExcel())</c> and inject
/// <see cref="IExportService"/> wherever an export is needed.
/// </remarks>
/// <example>
/// <code>
/// var result = await exportService.ExportAsync(reportData, ExportFormat.Pdf, options, cancellationToken);
/// return File(result.Stream, result.ContentType, result.FileName);
/// </code>
/// </example>
public interface IExportService
{
    /// <summary>
    /// Gets the formats that have a registered exporter.
    /// </summary>
    IReadOnlyCollection<ExportFormat> SupportedFormats { get; }

    /// <summary>
    /// Determines whether a format has a registered exporter.
    /// </summary>
    /// <param name="format">The format to probe.</param>
    /// <returns><see langword="true"/> when the format can be exported.</returns>
    bool Supports(ExportFormat format);

    /// <summary>
    /// Gets the exporter registered for a format.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The exporter.</returns>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    IExporter GetExporter(ExportFormat format);

    /// <summary>
    /// Exports a request and returns the content as a ready-to-serve result.
    /// </summary>
    /// <param name="request">The data, document and options to render.</param>
    /// <param name="format">The target format.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>The export result. Dispose it, or hand it to a framework that does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    Task<ExportResult> ExportAsync(ExportRequest request, ExportFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a sequence of DTOs, deriving the table layout from the item type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="data">The already-prepared data to export.</param>
    /// <param name="format">The target format.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>The export result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    Task<ExportResult> ExportAsync<T>(
        IEnumerable<T> data,
        ExportFormat format,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a pre-built document.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="format">The target format.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>The export result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    Task<ExportResult> ExportAsync(
        ExportDocument document,
        ExportFormat format,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an export directly to a destination stream. This is the lowest-allocation path because
    /// the content is never buffered in memory.
    /// </summary>
    /// <param name="request">The data, document and options to render.</param>
    /// <param name="format">The target format.</param>
    /// <param name="destination">The stream that receives the content; it is not disposed.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A task that completes when all content has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    Task ExportToAsync(
        ExportRequest request,
        ExportFormat format,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the file name an export would use, without running the export.
    /// </summary>
    /// <param name="request">The request whose options and document supply the name.</param>
    /// <param name="format">The target format.</param>
    /// <returns>The sanitized file name including extension.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">No exporter is registered for the format.</exception>
    string GetFileName(ExportRequest request, ExportFormat format);
}
