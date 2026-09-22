using Albin.Export.Abstractions;

namespace Albin.Export.Chart;

/// <summary>
/// Exports a chart as an Apache ECharts <c>option</c> object in JSON form.
/// </summary>
/// <remarks>
/// <para>
/// Use this when the chart should be rendered interactively by the client: the server ships data and
/// configuration only, and the browser calls <c>echarts.init(el).setOption(option)</c>. Keeping
/// rendering on the client avoids pulling a browser automation stack onto the server.
/// </para>
/// <code language="javascript">
/// const option = await (await fetch('/reports/revenue/echarts')).json();
/// echarts.init(document.getElementById('chart')).setOption(option);
/// </code>
/// </remarks>
public sealed class EChartsExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.ECharts;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Json;

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var spec = await ChartSpecResolver.ResolveAsync(request, cancellationToken);
        var indent = request.Options is not ChartExportOptions options || options.IndentJson;
        var json = EChartsOptionBuilder.ToJson(spec, indent);

        await ExportStreams.WriteTextAsync(destination, json, byteOrderMark: false, cancellationToken);
    }
}
