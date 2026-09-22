using Albin.Export.Abstractions;

namespace Albin.Export.Chart;

/// <summary>
/// Exports a chart as a PNG image.
/// </summary>
/// <remarks>
/// The image is rasterized in managed code, so no headless browser, native imaging library or font
/// installation is required. That keeps containers small and output identical across platforms, at
/// the cost of the typographic polish a browser would give you: use <see cref="SvgExporter"/> when
/// vector output is acceptable.
/// </remarks>
public sealed class PngExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Png;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Png;

    /// <inheritdoc />
    public string FileExtension => ".png";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var spec = await ChartSpecResolver.ResolveAsync(request, cancellationToken);
        var capacityHint = Math.Clamp(spec.Width * spec.Height / 8, 16 * 1024, 4 * 1024 * 1024);

        await ExportStreams
            .WriteSeekableAsync(destination, stream => ChartPngRenderer.Render(spec, stream), capacityHint, cancellationToken);
    }
}
