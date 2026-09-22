namespace Albin.Export.Pdf;

/// <summary>
/// Registration helper for the PDF exporter.
/// </summary>
public static class PdfExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="PdfExporter"/> and installs the cross-platform font resolver.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddPdf(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<PdfExporter>();
    }
}
