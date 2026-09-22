namespace Albin.Export.Html;

/// <summary>
/// Registration helper for the HTML exporter.
/// </summary>
public static class HtmlExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="HtmlExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddHtml(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<HtmlExporter>();
    }
}
