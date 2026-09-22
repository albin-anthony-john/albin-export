using System.Text;
using System.Text.Encodings.Web;
using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Documents;

namespace Albin.Export.Html;

/// <summary>
/// Exports a document as a clean, responsive, print-friendly HTML document or fragment.
/// </summary>
/// <remarks>
/// <para>
/// Every value that comes from data — titles, headings, paragraphs, headers and cell text — is passed
/// through <see cref="HtmlEncoder"/>, so exported content can never introduce markup or script into
/// the page. The generated document contains no inline event handlers and no network requests unless
/// you opt into client-side ECharts with <see cref="HtmlExportOptions.EChartsScriptUrl"/>.
/// </para>
/// <para>
/// Rows are written to the output stream as they are read, so a large table does not build a large
/// string in memory. Set <see cref="HtmlExportOptions.Fragment"/> to get only the body markup when
/// rendering inside a Razor layout; see <c>Albin.Export.AspNetCore</c> for rendering a Razor view
/// into an <c>ExportResult</c> instead.
/// </para>
/// </remarks>
public sealed class HtmlExporter : IExporter
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Html;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Html;

    /// <inheritdoc />
    public string FileExtension => ".html";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as HtmlExportOptions ?? new HtmlExportOptions();
        var document = request.Document;

        var writer = ExportStreams.CreateWriter(destination);
        await using (writer)
        {
            writer.NewLine = "\n";

            if (!options.Fragment)
            {
                await WriteHeadAsync(writer, document, options);
            }

            await writer.WriteLineAsync("<div class=\"export\">");

            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                await writer.WriteLineAsync($"<h1 class=\"export-title\">{Encode(document.Title)}</h1>")
                    ;
            }

            if (!string.IsNullOrWhiteSpace(document.Subtitle))
            {
                await writer.WriteLineAsync($"<p class=\"export-subtitle\">{Encode(document.Subtitle)}</p>")
                    ;
            }

            var chartIndex = 0;
            foreach (var section in document.Sections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (section is ExportChart)
                {
                    chartIndex++;
                }

                await WriteSectionAsync(writer, section, options, chartIndex, cancellationToken)
                    ;
            }

            if (!string.IsNullOrWhiteSpace(document.Footer))
            {
                await writer.WriteLineAsync($"<footer class=\"export-footer\">{Encode(document.Footer)}</footer>")
                    ;
            }

            await writer.WriteLineAsync("</div>");

            if (!options.Fragment)
            {
                if (options is { ChartMode: HtmlChartMode.EChartsClient, EChartsScriptUrl: { Length: > 0 } url }
                    && chartIndex > 0)
                {
                    await WriteEChartsBootstrapAsync(writer, url);
                }

                await writer.WriteLineAsync("</body>");
                await writer.WriteLineAsync("</html>");
            }

            await writer.FlushAsync(cancellationToken);
        }
    }

    private static async Task WriteHeadAsync(StreamWriter writer, ExportDocument document, HtmlExportOptions options)
    {
        await writer.WriteLineAsync("<!DOCTYPE html>");
        await writer.WriteLineAsync($"<html lang=\"{Encode(options.Language)}\">");
        await writer.WriteLineAsync("<head>");
        await writer.WriteLineAsync("<meta charset=\"utf-8\">");
        await writer.WriteLineAsync("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            ;
        await writer.WriteLineAsync($"<title>{Encode(document.Title ?? "Export")}</title>");

        if (!string.IsNullOrWhiteSpace(options.Author))
        {
            await writer.WriteLineAsync($"<meta name=\"author\" content=\"{Encode(options.Author)}\">")
                ;
        }

        if (options.IncludeDefaultStyles || !string.IsNullOrWhiteSpace(options.AdditionalCss))
        {
            await writer.WriteLineAsync("<style>");
            if (options.IncludeDefaultStyles)
            {
                await writer.WriteLineAsync(HtmlStyles.Default);
            }

            if (!string.IsNullOrWhiteSpace(options.AdditionalCss))
            {
                await writer.WriteLineAsync(options.AdditionalCss);
            }

            await writer.WriteLineAsync("</style>");
        }

        await writer.WriteLineAsync("</head>");
        await writer.WriteLineAsync("<body>");
    }

    private static async Task WriteSectionAsync(
        StreamWriter writer,
        ExportSection section,
        HtmlExportOptions options,
        int chartIndex,
        CancellationToken cancellationToken)
    {
        switch (section)
        {
            case ExportHeading heading:
                var level = Math.Clamp(heading.Level + 1, 2, 6);
                await writer.WriteLineAsync($"<h{level}>{Encode(heading.Text)}</h{level}>");
                break;

            case ExportParagraph paragraph:
                var cssClass = paragraph switch
                {
                    { Bold: true } => " class=\"bold\"",
                    { Muted: true } => " class=\"muted\"",
                    _ => string.Empty,
                };
                await writer.WriteLineAsync($"<p{cssClass}>{EncodeMultiline(paragraph.Text)}</p>")
                    ;
                break;

            case ExportTable table:
                await WriteTableAsync(writer, table, options, cancellationToken);
                break;

            case ExportChart chart when options.ChartMode != HtmlChartMode.None:
                await WriteChartAsync(writer, chart.Chart, options, chartIndex);
                break;

            case ExportImage image when !image.Data.IsEmpty:
                await WriteImageAsync(writer, image);
                break;

            case ExportPageBreak:
                await writer.WriteLineAsync("<div class=\"page-break\"></div>");
                break;

            default:
                // Unknown or empty sections are skipped so the model stays extensible.
                break;
        }
    }

    private static async Task WriteTableAsync(
        StreamWriter writer,
        ExportTable table,
        HtmlExportOptions options,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("<div class=\"table-wrap\">");
        await writer.WriteLineAsync("<table>");

        if (!string.IsNullOrWhiteSpace(table.Name))
        {
            await writer.WriteLineAsync($"<caption>{Encode(table.Name)}</caption>");
        }

        var classes = new string[table.Columns.Count];
        for (var i = 0; i < classes.Length; i++)
        {
            classes[i] = table.Columns[i].ResolveAlignment() switch
            {
                ExportAlignment.Right => " class=\"num\"",
                ExportAlignment.Center => " class=\"center\"",
                _ => string.Empty,
            };
        }

        if (options.IncludeHeaders && table.Columns.Count > 0)
        {
            await writer.WriteLineAsync("<thead><tr>");
            for (var i = 0; i < table.Columns.Count; i++)
            {
                await writer.WriteAsync($"<th{classes[i]}>{Encode(table.Columns[i].Header)}</th>")
                    ;
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("</tr></thead>");
        }

        await writer.WriteLineAsync("<tbody>");

        var rowNumber = 0;
        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            var stripe = options.StripedTables && rowNumber % 2 == 1 ? " class=\"stripe\"" : string.Empty;
            await writer.WriteAsync($"<tr{stripe}>");

            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < row.FieldCount ? row[i] : null;
                var text = ExportValueFormatter.Format(value, table.Columns[i], options);
                await writer.WriteAsync($"<td{classes[i]}>{Encode(text)}</td>");
            }

            await writer.WriteLineAsync("</tr>");
            rowNumber++;
        }

        await writer.WriteLineAsync("</tbody>");

        if (table.TotalsRow is { Count: > 0 } totals)
        {
            await writer.WriteAsync("<tfoot><tr>");
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < totals.Count ? totals[i] : null;
                var text = ExportValueFormatter.Format(value, table.Columns[i], options);
                await writer.WriteAsync($"<td{classes[i]}>{Encode(text)}</td>");
            }

            await writer.WriteLineAsync("</tr></tfoot>");
        }

        await writer.WriteLineAsync("</table>");

        if (rowNumber == 0)
        {
            await writer.WriteLineAsync("<p class=\"empty\">No data.</p>");
        }

        await writer.WriteLineAsync("</div>");
    }

    private static async Task WriteChartAsync(
        StreamWriter writer,
        ChartSpec chart,
        HtmlExportOptions options,
        int index)
    {
        await writer.WriteLineAsync("<figure>");

        if (options.ChartMode == HtmlChartMode.EChartsClient)
        {
            var json = EChartsOptionBuilder.ToJson(chart);
            await writer.WriteLineAsync(
                    $"<div class=\"echarts\" id=\"albin-chart-{index}\" "
                    + $"style=\"height:{chart.Height}px\" data-echarts-option=\"{Encode(json)}\"></div>")
                ;
        }
        else
        {
            var svg = new StringBuilder(4096);
            ChartSvgRenderer.Render(chart, svg);
            await writer.WriteLineAsync(svg.ToString());
        }

        if (!string.IsNullOrWhiteSpace(chart.Title))
        {
            await writer.WriteLineAsync($"<figcaption class=\"muted\">{Encode(chart.Title)}</figcaption>")
                ;
        }

        await writer.WriteLineAsync("</figure>");
    }

    private static async Task WriteImageAsync(StreamWriter writer, ExportImage image)
    {
        var base64 = Convert.ToBase64String(image.Data.Span);
        var size = new StringBuilder();
        if (image.Width is { } width)
        {
            size.Append("width:").Append((int)width).Append("px;");
        }

        if (image.Height is { } height)
        {
            size.Append("height:").Append((int)height).Append("px;");
        }

        var style = size.Length > 0 ? $" style=\"{size}\"" : string.Empty;
        await writer.WriteLineAsync(
                $"<figure><img src=\"data:{Encode(image.ContentType)};base64,{base64}\" "
                + $"alt=\"{Encode(image.AltText ?? string.Empty)}\"{style}></figure>")
            ;
    }

    private static Task WriteEChartsBootstrapAsync(StreamWriter writer, string scriptUrl)
    {
        // The loader is a fixed script; chart data travels in a data- attribute and is parsed as JSON,
        // never evaluated, so exported values cannot become executable code.
        var script = $$"""
            <script src="{{Encode(scriptUrl)}}"></script>
            <script>
            (function () {
              if (typeof echarts === 'undefined') { return; }
              document.querySelectorAll('[data-echarts-option]').forEach(function (el) {
                try {
                  echarts.init(el).setOption(JSON.parse(el.dataset.echartsOption));
                } catch (e) {
                  el.textContent = 'Chart could not be rendered.';
                }
              });
            })();
            </script>
            """;
        return writer.WriteLineAsync(script);
    }

    private static string Encode(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : Encoder.Encode(value);

    private static string EncodeMultiline(string? value)
        => Encode(value)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
}
