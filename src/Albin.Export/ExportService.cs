using System.Collections.Frozen;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export;

/// <summary>
/// The default <see cref="IExportService"/>: a thin facade that resolves the exporter for a format
/// and manages the output stream.
/// </summary>
/// <remarks>
/// The service is stateless and thread safe, and is registered as a singleton by
/// <c>AddAlbinExport</c>. It can also be constructed directly in applications that do not use
/// dependency injection.
/// </remarks>
public sealed class ExportService : IExportService
{
    private const int MinimumBufferCapacity = 16 * 1024;
    private const int MaximumBufferCapacity = 8 * 1024 * 1024;

    private readonly FrozenDictionary<ExportFormat, IExporter> _exporters;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="exporters">
    /// The available exporters. When two exporters claim the same format the last one wins, which
    /// lets an application replace a built-in exporter with its own.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="exporters"/> is <see langword="null"/>.</exception>
    public ExportService(IEnumerable<IExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters);

        var map = new Dictionary<ExportFormat, IExporter>();
        foreach (var exporter in exporters)
        {
            if (exporter is not null)
            {
                map[exporter.Format] = exporter;
            }
        }

        _exporters = map.ToFrozenDictionary();
        SupportedFormats = [.. _exporters.Keys.Order()];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ExportFormat> SupportedFormats { get; }

    /// <inheritdoc />
    public bool Supports(ExportFormat format) => _exporters.ContainsKey(format);

    /// <inheritdoc />
    public IExporter GetExporter(ExportFormat format)
        => _exporters.TryGetValue(format, out var exporter)
            ? exporter
            : throw new ExportFormatNotSupportedException(format, SupportedFormats);

    /// <inheritdoc />
    public async Task<ExportResult> ExportAsync(
        ExportRequest request,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var exporter = GetExporter(format);

        var buffer = ExportStreams.CreateBuffer(EstimateCapacity(request));
        try
        {
            await exporter.ExportAsync(request, buffer, cancellationToken);
            buffer.Position = 0;
            return new ExportResult(
                request.ResolveFileNameStem() + exporter.FileExtension,
                exporter.ContentType,
                buffer,
                format);
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public Task<ExportResult> ExportAsync<T>(
        IEnumerable<T> data,
        ExportFormat format,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return ExportAsync(ExportRequest.FromItems(data, options), format, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExportResult> ExportAsync(
        ExportDocument document,
        ExportFormat format,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ExportAsync(new ExportRequest(document, options), format, cancellationToken);
    }

    /// <inheritdoc />
    public Task ExportToAsync(
        ExportRequest request,
        ExportFormat format,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        return GetExporter(format).ExportAsync(request, destination, cancellationToken);
    }

    /// <inheritdoc />
    public string GetFileName(ExportRequest request, ExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(request);
        var exporter = GetExporter(format);
        return ExportFileName.Sanitize(request.ResolveFileNameStem(), exporter.FileExtension);
    }

    private static int EstimateCapacity(ExportRequest request)
    {
        var cells = 0L;
        foreach (var table in request.Document.Tables())
        {
            var rows = table.Rows.Count ?? 256;
            cells += (long)rows * Math.Max(table.Columns.Count, 1);
        }

        // ~24 bytes of serialized output per cell is a good first approximation for text formats
        // and keeps the buffer from growing repeatedly for the binary ones.
        var estimate = cells * 24L;
        return (int)Math.Clamp(estimate, MinimumBufferCapacity, MaximumBufferCapacity);
    }
}
