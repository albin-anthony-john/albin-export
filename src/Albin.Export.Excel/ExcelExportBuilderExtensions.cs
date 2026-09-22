namespace Albin.Export.Excel;

/// <summary>
/// Registration helper for the Excel exporter.
/// </summary>
public static class ExcelExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ExcelExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddExcel(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<ExcelExporter>();
    }
}
