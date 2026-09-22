using Albin.Export.Abstractions;

namespace Albin.Export.AspNetCore.Razor;

/// <summary>
/// Renders a Razor view or page to HTML outside the normal request pipeline, so a report template can
/// become a downloadable file.
/// </summary>
/// <remarks>
/// This is the "reusable template" half of HTML export: the layout lives in a <c>.cshtml</c> file with
/// a strongly typed model, editable by anyone who knows Razor, while the core library stays free of
/// any view engine dependency.
/// </remarks>
public interface IRazorExportRenderer
{
    /// <summary>
    /// Renders a view to an HTML string.
    /// </summary>
    /// <param name="viewName">
    /// The view name or path, for example <c>Reports/Invoice</c> or
    /// <c>~/Views/Reports/Invoice.cshtml</c>.
    /// </param>
    /// <param name="model">The view model.</param>
    /// <param name="cancellationToken">Token used to cancel rendering.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="ExportException">The view could not be found.</exception>
    Task<string> RenderAsync(string viewName, object? model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a view and wraps the HTML in an export result ready to be returned from a controller.
    /// </summary>
    /// <param name="viewName">The view name or path.</param>
    /// <param name="model">The view model.</param>
    /// <param name="fileName">The download file name, with or without the <c>.html</c> extension.</param>
    /// <param name="cancellationToken">Token used to cancel rendering.</param>
    /// <returns>An HTML export result.</returns>
    /// <exception cref="ExportException">The view could not be found.</exception>
    Task<ExportResult> RenderToResultAsync(
        string viewName,
        object? model,
        string? fileName = null,
        CancellationToken cancellationToken = default);
}
