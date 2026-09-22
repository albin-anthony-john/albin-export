namespace Albin.Export.Chart;

/// <summary>
/// Registration helpers for the chart exporters.
/// </summary>
public static class ChartExportBuilderExtensions
{
    /// <summary>
    /// Registers the SVG, PNG and ECharts exporters.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddCharts(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddExporter<SvgExporter>()
            .AddExporter<PngExporter>()
            .AddExporter<EChartsExporter>();
    }

    /// <summary>
    /// Registers only the SVG chart exporter.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddSvg(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<SvgExporter>();
    }

    /// <summary>
    /// Registers only the PNG chart exporter.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddPng(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<PngExporter>();
    }

    /// <summary>
    /// Registers only the Apache ECharts option exporter.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddECharts(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExporter<EChartsExporter>();
    }
}
