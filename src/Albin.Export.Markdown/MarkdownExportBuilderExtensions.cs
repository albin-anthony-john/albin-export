namespace Albin.Export.Markdown;

/// <summary>
/// Registration helper for the Markdown exporter.
/// </summary>
public static class MarkdownExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="MarkdownExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddMarkdown(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<MarkdownExporter>();
    }
}
