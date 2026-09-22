using Albin.Export.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Albin.Export.AspNetCore;

/// <summary>
/// Writes exports straight to the HTTP response.
/// </summary>
public static class ExportHttpExtensions
{
    /// <summary>
    /// Exports directly into the response body without buffering the file in memory first.
    /// </summary>
    /// <param name="exportService">The export service.</param>
    /// <param name="response">The HTTP response to write to.</param>
    /// <param name="request">The data, document and options to render.</param>
    /// <param name="format">The target format.</param>
    /// <param name="inline">
    /// When <see langword="true"/> the <c>Content-Disposition</c> header asks the browser to display
    /// the file instead of downloading it.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A task that completes when the response body has been written.</returns>
    /// <remarks>
    /// <para>
    /// This is the lowest-allocation way to serve an export: the streaming formats (CSV, JSON, HTML,
    /// XML, Markdown, SVG) write their bytes straight to the socket, so peak memory does not grow
    /// with the size of the export. The document formats still need a buffer internally because their
    /// underlying libraries require a seekable stream.
    /// </para>
    /// <para>
    /// Because the headers are sent before rendering starts, an error thrown mid-render cannot be
    /// turned into an error status code. Use it for large, well-understood exports and the buffered
    /// <see cref="IExportService.ExportAsync(ExportRequest, ExportFormat, CancellationToken)"/> for
    /// everything else.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any required argument is <see langword="null"/>.</exception>
    public static async Task ExportToResponseAsync(
        this IExportService exportService,
        HttpResponse response,
        ExportRequest request,
        ExportFormat format,
        bool inline = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);

        var exporter = exportService.GetExporter(format);
        var fileName = exportService.GetFileName(request, format);

        response.ContentType = exporter.ContentType;
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
        {
            FileNameStar = fileName,
        }.ToString();

        await exportService
            .ExportToAsync(request, format, response.Body, cancellationToken)
            ;

        await response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Exports a sequence of DTOs directly into the response body.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="exportService">The export service.</param>
    /// <param name="response">The HTTP response to write to.</param>
    /// <param name="data">The already-prepared data to export.</param>
    /// <param name="format">The target format.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A task that completes when the response body has been written.</returns>
    /// <exception cref="ArgumentNullException">Any required argument is <see langword="null"/>.</exception>
    public static Task ExportToResponseAsync<T>(
        this IExportService exportService,
        HttpResponse response,
        IEnumerable<T> data,
        ExportFormat format,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(data);
        return exportService.ExportToResponseAsync(
            response,
            ExportRequest.FromItems(data, options),
            format,
            inline: false,
            cancellationToken);
    }
}
