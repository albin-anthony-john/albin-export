using Albin.Export.Abstractions;

namespace Albin.Export.AspNetCore.Razor;

/// <summary>
/// Options that point the Razor HTML exporter at a view.
/// </summary>
public sealed class RazorExportOptions : ExportOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RazorExportOptions"/> class.
    /// </summary>
    /// <param name="viewName">The Razor view name or path to render.</param>
    /// <exception cref="ArgumentException"><paramref name="viewName"/> is <see langword="null"/> or whitespace.</exception>
    public RazorExportOptions(string viewName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        ViewName = viewName;
    }

    /// <summary>Gets the Razor view name or path.</summary>
    public string ViewName { get; }

    /// <summary>
    /// Gets or sets the view model. When <see langword="null"/> the exporter passes
    /// <see cref="ExportRequest.Data"/>, falling back to <see cref="ExportRequest.Document"/>.
    /// </summary>
    public object? Model { get; set; }
}

/// <summary>
/// Renders <see cref="ExportFormat.Html"/> through a Razor view instead of the built-in HTML writer.
/// </summary>
/// <remarks>
/// Register it with <c>export.AddRazorHtml()</c>. Because a format resolves to exactly one exporter,
/// registering this after <c>AddHtml()</c> replaces the built-in HTML exporter, letting an application
/// move to its own templates without touching call sites. The request must carry
/// <see cref="RazorExportOptions"/> so the exporter knows which view to render.
/// </remarks>
public sealed class RazorHtmlExporter : IExporter
{
    private readonly IRazorExportRenderer _renderer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RazorHtmlExporter"/> class.
    /// </summary>
    /// <param name="renderer">The Razor renderer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is <see langword="null"/>.</exception>
    public RazorHtmlExporter(IRazorExportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Html;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Html;

    /// <inheritdoc />
    public string FileExtension => ".html";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        if (request.Options is not RazorExportOptions options)
        {
            throw new ExportException(
                "Razor HTML export needs RazorExportOptions with a view name. Pass "
                + "new RazorExportOptions(\"Reports/Invoice\") { Model = model } as the export options.");
        }

        var model = options.Model ?? request.Data ?? request.Document;
        var html = await _renderer.RenderAsync(options.ViewName, model, cancellationToken);

        await ExportStreams.WriteTextAsync(destination, html, byteOrderMark: false, cancellationToken)
            ;
    }
}
