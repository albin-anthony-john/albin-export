using Albin.Export.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Albin.Export.AspNetCore;

/// <summary>
/// Turns an <see cref="ExportResult"/> into an ASP.NET Core response.
/// </summary>
public static class ExportResultExtensions
{
    /// <summary>
    /// Converts the result into an MVC file result for use from a controller.
    /// </summary>
    /// <param name="result">The export result. The framework disposes its stream after the response is sent.</param>
    /// <returns>A file result carrying the content, media type and download name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var result = await exportService.ExportAsync(data, ExportFormat.Excel, options, cancellationToken);
    /// return result.ToFileResult();
    ///
    /// // equivalent to the plain MVC form:
    /// return File(result.Stream, result.ContentType, result.FileName);
    /// </code>
    /// </example>
    public static FileStreamResult ToFileResult(this ExportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new FileStreamResult(result.Stream, result.ContentType)
        {
            FileDownloadName = result.FileName,
            EnableRangeProcessing = false,
        };
    }

    /// <summary>
    /// Converts the result into a minimal API result.
    /// </summary>
    /// <param name="result">The export result. The framework disposes its stream after the response is sent.</param>
    /// <param name="inline">
    /// When <see langword="true"/> the file is shown in the browser instead of downloaded, which is
    /// handy for PDF and HTML previews.
    /// </param>
    /// <returns>A result carrying the content, media type and download name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// app.MapGet("/reports/sales.xlsx", async (IExportService export, CancellationToken token) =&gt;
    /// {
    ///     var result = await export.ExportAsync(rows, ExportFormat.Excel, cancellationToken: token);
    ///     return result.ToHttpResult();
    /// });
    /// </code>
    /// </example>
    public static IResult ToHttpResult(this ExportResult result, bool inline = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Results.File(
            result.Stream,
            result.ContentType,
            inline ? null : result.FileName,
            enableRangeProcessing: false);
    }
}
