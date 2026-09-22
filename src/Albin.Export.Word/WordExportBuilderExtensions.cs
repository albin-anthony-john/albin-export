namespace Albin.Export.Word;

/// <summary>
/// Registration helper for the Word exporter.
/// </summary>
public static class WordExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="WordExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddWord(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<WordExporter>();
    }
}
