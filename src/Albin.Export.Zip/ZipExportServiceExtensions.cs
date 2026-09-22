using System.IO.Compression;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Zip;

/// <summary>
/// Bundling helpers that export the same data in several formats and zip the results.
/// </summary>
public static class ZipExportServiceExtensions
{
    /// <summary>
    /// Exports a request in several formats and returns them as one ZIP archive.
    /// </summary>
    /// <param name="exportService">The export service.</param>
    /// <param name="request">The data, document and options to render.</param>
    /// <param name="formats">The formats to include. Each needs a registered exporter.</param>
    /// <param name="compressionLevel">The compression level.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A result whose stream is a ZIP archive containing one file per format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exportService"/>, <paramref name="request"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="formats"/> is empty.</exception>
    /// <exception cref="ExportFormatNotSupportedException">One of the formats has no registered exporter.</exception>
    /// <example>
    /// <code>
    /// var zip = await exportService.ExportBundleAsync(
    ///     request,
    ///     [ExportFormat.Pdf, ExportFormat.Excel, ExportFormat.Csv],
    ///     cancellationToken: cancellationToken);
    ///
    /// return File(zip.Stream, zip.ContentType, zip.FileName);
    /// </code>
    /// </example>
    public static async Task<ExportResult> ExportBundleAsync(
        this IExportService exportService,
        ExportRequest request,
        IReadOnlyList<ExportFormat> formats,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(formats);
        if (formats.Count == 0)
        {
            throw new ArgumentException("At least one format is required.", nameof(formats));
        }

        // Fail fast before any work happens.
        foreach (var format in formats)
        {
            _ = exportService.GetExporter(format);
        }

        var results = new List<ExportResult>(formats.Count);
        try
        {
            foreach (var format in formats)
            {
                results.Add(await exportService.ExportAsync(request, format, cancellationToken));
            }

            var archive = ExportStreams.CreateBuffer(64 * 1024);
            try
            {
                await ZipExporter
                    .WriteAsync(archive, results.Select(ZipEntrySource.FromResult), compressionLevel, cancellationToken)
                    ;

                archive.Position = 0;
                return new ExportResult(
                    request.ResolveFileNameStem() + ".zip",
                    ExportContentTypes.Zip,
                    archive,
                    ExportFormat.Zip);
            }
            catch
            {
                await archive.DisposeAsync();
                throw;
            }
        }
        finally
        {
            foreach (var result in results)
            {
                await result.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Exports a sequence of DTOs in several formats and returns them as one ZIP archive.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="exportService">The export service.</param>
    /// <param name="data">The already-prepared data to export.</param>
    /// <param name="formats">The formats to include.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A result whose stream is a ZIP archive containing one file per format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exportService"/>, <paramref name="data"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
    public static Task<ExportResult> ExportBundleAsync<T>(
        this IExportService exportService,
        IEnumerable<T> data,
        IReadOnlyList<ExportFormat> formats,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(data);

        // Materialized because each format enumerates the data once.
        var items = data as IReadOnlyCollection<T> ?? data.ToArray();
        return exportService.ExportBundleAsync(
            ExportRequest.FromItems(items, options),
            formats,
            CompressionLevel.Optimal,
            cancellationToken);
    }

    /// <summary>
    /// Exports a pre-built document in several formats and returns them as one ZIP archive.
    /// </summary>
    /// <param name="exportService">The export service.</param>
    /// <param name="document">The document to render.</param>
    /// <param name="formats">The formats to include.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A result whose stream is a ZIP archive containing one file per format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exportService"/>, <paramref name="document"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
    public static Task<ExportResult> ExportBundleAsync(
        this IExportService exportService,
        ExportDocument document,
        IReadOnlyList<ExportFormat> formats,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(document);
        return exportService.ExportBundleAsync(
            new ExportRequest(document, options),
            formats,
            CompressionLevel.Optimal,
            cancellationToken);
    }

    /// <summary>
    /// Creates a ZIP archive from arbitrary entries.
    /// </summary>
    /// <param name="exportService">The export service (used only for discoverability of this helper).</param>
    /// <param name="fileName">The archive file name, with or without the <c>.zip</c> extension.</param>
    /// <param name="entries">The entries to add.</param>
    /// <param name="compressionLevel">The compression level.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A result whose stream is the ZIP archive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exportService"/> or <paramref name="entries"/> is <see langword="null"/>.</exception>
    public static async Task<ExportResult> CreateArchiveAsync(
        this IExportService exportService,
        string fileName,
        IEnumerable<ZipEntrySource> entries,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(entries);

        var archive = ExportStreams.CreateBuffer(64 * 1024);
        try
        {
            await ZipExporter.WriteAsync(archive, entries, compressionLevel, cancellationToken);
            archive.Position = 0;
            return new ExportResult(
                ExportFileName.Sanitize(fileName, ".zip"),
                ExportContentTypes.Zip,
                archive,
                ExportFormat.Zip);
        }
        catch
        {
            await archive.DisposeAsync();
            throw;
        }
    }
}

/// <summary>
/// Registration helper for the ZIP exporter.
/// </summary>
public static class ZipExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ZipExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddZip(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<ZipExporter>();
    }
}
