namespace Albin.Export.Xml;

/// <summary>
/// Registration helper for the XML exporter.
/// </summary>
public static class XmlExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="XmlExporter"/>.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddXml(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<XmlExporter>();
    }
}
