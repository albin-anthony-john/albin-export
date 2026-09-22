using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Markdown;

/// <summary>
/// Exports a document as GitHub flavoured Markdown.
/// </summary>
/// <remarks>
/// Written by hand against a <see cref="StreamWriter"/> rather than through a Markdown library: the
/// subset a report needs (headings, paragraphs, pipe tables, images) is small, and avoiding a
/// dependency keeps the package weightless. Cell content is escaped so a value containing <c>|</c>,
/// a line break or a leading <c>#</c> cannot break out of its cell.
/// </remarks>
public sealed class MarkdownExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Markdown;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Markdown;

    /// <inheritdoc />
    public string FileExtension => ".md";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as MarkdownExportOptions ?? new MarkdownExportOptions();
        var document = request.Document;

        var writer = ExportStreams.CreateWriter(destination);
        await using (writer)
        {
            writer.NewLine = "\n";

            if (options.IncludeTitle && !string.IsNullOrWhiteSpace(document.Title))
            {
                await writer.WriteLineAsync("# " + EscapeText(document.Title));
                await writer.WriteLineAsync();
            }

            if (!string.IsNullOrWhiteSpace(document.Subtitle))
            {
                await writer.WriteLineAsync("_" + EscapeText(document.Subtitle) + "_");
                await writer.WriteLineAsync();
            }

            foreach (var section in document.Sections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteSectionAsync(writer, section, options, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(document.Footer))
            {
                await writer.WriteLineAsync("---");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync(EscapeText(document.Footer));
            }

            await writer.FlushAsync(cancellationToken);
        }
    }

    private static async Task WriteSectionAsync(
        StreamWriter writer,
        ExportSection section,
        MarkdownExportOptions options,
        CancellationToken cancellationToken)
    {
        switch (section)
        {
            case ExportHeading heading:
                await writer.WriteLineAsync(new string('#', heading.Level) + " " + EscapeText(heading.Text))
                    ;
                await writer.WriteLineAsync();
                break;

            case ExportParagraph paragraph:
                var text = EscapeText(paragraph.Text);
                if (paragraph.Bold)
                {
                    text = "**" + text + "**";
                }
                else if (paragraph.Muted)
                {
                    text = "_" + text + "_";
                }

                await writer.WriteLineAsync(text);
                await writer.WriteLineAsync();
                break;

            case ExportTable table:
                await WriteTableAsync(writer, table, options, cancellationToken);
                break;

            case ExportChart chart when options.ChartsAsTables:
                await WriteChartAsync(writer, chart.Chart, options);
                break;

            case ExportImage image:
                await WriteImageAsync(writer, image, options);
                break;

            case ExportPageBreak:
                await writer.WriteLineAsync("---");
                await writer.WriteLineAsync();
                break;

            default:
                // Unknown section types are skipped so the model stays extensible.
                break;
        }
    }

    private static async Task WriteTableAsync(
        StreamWriter writer,
        ExportTable table,
        MarkdownExportOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(table.Name))
        {
            await writer.WriteLineAsync("### " + EscapeText(table.Name));
            await writer.WriteLineAsync();
        }

        if (table.Columns.Count == 0)
        {
            return;
        }

        if (options.IncludeHeaders)
        {
            await WriteRowAsync(writer, table.Columns.Select(static c => EscapeCell(c.Header)));
            await WriteRowAsync(writer, table.Columns.Select(c => AlignmentMarker(c, options)));
        }

        var cells = new string[table.Columns.Count];
        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var value = i < row.FieldCount ? row[i] : null;
                cells[i] = EscapeCell(ExportValueFormatter.Format(value, table.Columns[i], options));
            }

            await WriteRowAsync(writer, cells);
        }

        if (table.TotalsRow is { Count: > 0 } totals)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var value = i < totals.Count ? totals[i] : null;
                cells[i] = "**" + EscapeCell(ExportValueFormatter.Format(value, table.Columns[i], options)) + "**";
            }

            await WriteRowAsync(writer, cells);
        }

        await writer.WriteLineAsync();
    }

    private static async Task WriteChartAsync(StreamWriter writer, ChartSpec chart, MarkdownExportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(chart.Title))
        {
            await writer.WriteLineAsync("### " + EscapeText(chart.Title));
            await writer.WriteLineAsync();
        }

        var headers = new List<string> { EscapeCell(chart.XAxisLabel ?? "Category") };
        headers.AddRange(chart.Series.Select(static s => EscapeCell(s.Name)));
        await WriteRowAsync(writer, headers);
        await WriteRowAsync(writer, headers.Select(static (_, i) => i == 0 ? "---" : "---:"));

        for (var c = 0; c < chart.Categories.Count; c++)
        {
            var cells = new List<string>(chart.Series.Count + 1) { EscapeCell(chart.Categories[c]) };
            foreach (var series in chart.Series)
            {
                var value = c < series.Values.Count ? series.Values[c] : null;
                cells.Add(value?.ToString(options.Culture) ?? options.NullText);
            }

            await WriteRowAsync(writer, cells);
        }

        await writer.WriteLineAsync();
    }

    private static async Task WriteImageAsync(StreamWriter writer, ExportImage image, MarkdownExportOptions options)
    {
        var alt = EscapeCell(image.AltText ?? "image");
        if (options.EmbedImages && !image.Data.IsEmpty)
        {
            var base64 = Convert.ToBase64String(image.Data.Span);
            await writer.WriteLineAsync($"![{alt}](data:{image.ContentType};base64,{base64})");
        }
        else
        {
            await writer.WriteLineAsync($"_[{alt}]_");
        }

        await writer.WriteLineAsync();
    }

    private static Task WriteRowAsync(StreamWriter writer, IEnumerable<string> cells)
        => writer.WriteLineAsync("| " + string.Join(" | ", cells) + " |");

    private static string AlignmentMarker(ExportColumn column, MarkdownExportOptions options)
    {
        if (!options.IncludeAlignmentMarkers)
        {
            return "---";
        }

        return column.ResolveAlignment() switch
        {
            ExportAlignment.Right => "---:",
            ExportAlignment.Center => ":---:",
            _ => ":---",
        };
    }

    private static string EscapeText(string? value)
        => value is null ? string.Empty : value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string EscapeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal);
    }
}
