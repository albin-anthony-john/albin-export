using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Documents;

namespace Albin.Export.Word;

/// <summary>Base class for the materialized blocks handed to the Open XML writer.</summary>
internal abstract record WordBlock;

internal sealed record WordHeadingBlock(string Text, int Level) : WordBlock;

internal sealed record WordParagraphBlock(string Text, bool Bold, bool Muted) : WordBlock;

internal sealed record WordTableBlock(ExportTableSnapshot Table, bool IncludeHeaders) : WordBlock;

internal sealed record WordImageBlock(
    byte[] Data,
    string ContentType,
    double? Width,
    double? Height,
    string? AltText) : WordBlock;

internal sealed record WordPageBreakBlock : WordBlock;

/// <summary>
/// Turns the asynchronous document model into synchronous blocks.
/// </summary>
/// <remarks>
/// The Open XML object model has no asynchronous API, so rows are streamed and formatted here — with
/// cancellation honoured — before the package is assembled. That keeps all the awaiting in one place
/// instead of blocking inside the writer.
/// </remarks>
internal sealed class WordContent
{
    private WordContent(List<WordBlock> blocks) => Blocks = blocks;

    public List<WordBlock> Blocks { get; }

    public static async ValueTask<WordContent> BuildAsync(
        ExportRequest request,
        WordExportOptions options,
        CancellationToken cancellationToken)
    {
        var blocks = new List<WordBlock>(request.Document.Sections.Count + 2);

        foreach (var section in request.Document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (section)
            {
                case ExportHeading heading:
                    blocks.Add(new WordHeadingBlock(heading.Text, heading.Level + 1));
                    break;

                case ExportParagraph paragraph:
                    blocks.Add(new WordParagraphBlock(paragraph.Text, paragraph.Bold, paragraph.Muted));
                    break;

                case ExportTable table:
                    var snapshot = await ExportTableSnapshot
                        .CreateAsync(table, options, cancellationToken)
                        ;
                    blocks.Add(new WordTableBlock(snapshot, options.IncludeHeaders));
                    break;

                case ExportChart chart:
                    blocks.Add(new WordImageBlock(
                        ChartPngRenderer.Render(chart.Chart),
                        "image/png",
                        chart.Chart.Width,
                        chart.Chart.Height,
                        chart.Chart.Title));
                    break;

                case ExportImage image when !image.Data.IsEmpty:
                    blocks.Add(new WordImageBlock(
                        image.Data.ToArray(),
                        image.ContentType,
                        image.Width,
                        image.Height,
                        image.AltText));
                    break;

                case ExportPageBreak:
                    blocks.Add(new WordPageBreakBlock());
                    break;

                default:
                    // Unknown section types are skipped so the model stays extensible.
                    break;
            }
        }

        return new WordContent(blocks);
    }
}
