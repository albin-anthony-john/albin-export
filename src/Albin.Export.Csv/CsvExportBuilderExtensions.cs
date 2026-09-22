namespace Albin.Export.Csv;

/// <summary>
/// Registration helper for the CSV exporter.
/// </summary>
public static class CsvExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="CsvExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddCsv(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<CsvExporter>();
    }
}
