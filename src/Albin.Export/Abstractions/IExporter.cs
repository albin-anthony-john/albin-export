namespace Albin.Export.Abstractions;

/// <summary>
/// Renders an <see cref="ExportRequest"/> into one concrete file format.
/// </summary>
/// <remarks>
/// <para>
/// Exporters write straight into a caller supplied stream, which lets the framework stream to an
/// HTTP response without an intermediate buffer. Implementations must:
/// </para>
/// <list type="bullet">
///   <item><description>never dispose or close <c>destination</c>;</description></item>
///   <item><description>honour the cancellation token;</description></item>
///   <item><description>be thread safe, because a single instance is registered as a singleton.</description></item>
/// </list>
/// <para>
/// See the <c>Extending</c> documentation for a worked custom exporter.
/// </para>
/// </remarks>
public interface IExporter
{
    /// <summary>Gets the format this exporter produces.</summary>
    ExportFormat Format { get; }

    /// <summary>Gets the media type of the produced content, for example <c>application/pdf</c>.</summary>
    string ContentType { get; }

    /// <summary>Gets the file extension of the produced content, including the leading dot.</summary>
    string FileExtension { get; }

    /// <summary>
    /// Writes the export to <paramref name="destination"/>.
    /// </summary>
    /// <param name="request">The data, document and options to render.</param>
    /// <param name="destination">
    /// The stream that receives the content. The exporter writes but does not dispose it.
    /// Formats that need random access (ZIP based Office formats) buffer internally when the
    /// destination is not seekable.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A task that completes when all content has been written.</returns>
    Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default);
}
