namespace Albin.Export.Json;

/// <summary>
/// Registration helper for the JSON exporter.
/// </summary>
public static class JsonExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="JsonExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddJson(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<JsonExporter>();
    }
}
