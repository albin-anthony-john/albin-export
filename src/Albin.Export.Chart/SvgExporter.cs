using Albin.Export.Abstractions;

namespace Albin.Export.Chart;

/// <summary>
/// Exports a chart as a standalone SVG document.
/// </summary>
/// <remarks>
/// SVG is the recommended server-side chart format: it is generated in pure managed code, scales
/// without loss, prints cleanly and contains no scripts.
/// </remarks>
public sealed class SvgExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Svg;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Svg;

    /// <inheritdoc />
    public string FileExtension => ".svg";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var spec = await ChartSpecResolver.ResolveAsync(request, cancellationToken);
        var svg = ChartSvgRenderer.Render(spec);
        await ExportStreams.WriteTextAsync(destination, svg, byteOrderMark: false, cancellationToken);
    }
}
