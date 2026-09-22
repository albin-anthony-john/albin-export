using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Documents;

namespace Albin.Export.Pdf;

/// <summary>Base class for the materialized blocks handed to the MigraDoc writer.</summary>
internal abstract record PdfBlock;

internal sealed record PdfHeadingBlock(string Text, int Level) : PdfBlock;

internal sealed record PdfParagraphBlock(string Text, bool Bold, bool Muted) : PdfBlock;

internal sealed record PdfTableBlock(ExportTableSnapshot Table, bool IncludeHeaders) : PdfBlock;

internal sealed record PdfImageBlock(byte[] Data, double? Width) : PdfBlock;

internal sealed record PdfPageBreakBlock : PdfBlock;

/// <summary>
/// Reads the document asynchronously so the synchronous MigraDoc model can be built without blocking.
/// </summary>
internal sealed class PdfContent
{
    private PdfContent(List<PdfBlock> blocks) => Blocks = blocks;

    public List<PdfBlock> Blocks { get; }

    public static async ValueTask<PdfContent> BuildAsync(
        ExportRequest request,
        PdfExportOptions options,
        CancellationToken cancellationToken)
    {
        var blocks = new List<PdfBlock>(request.Document.Sections.Count + 2);

        foreach (var section in request.Document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (section)
            {
                case ExportHeading heading:
                    blocks.Add(new PdfHeadingBlock(heading.Text, heading.Level + 1));
                    break;

                case ExportParagraph paragraph:
                    blocks.Add(new PdfParagraphBlock(paragraph.Text, paragraph.Bold, paragraph.Muted));
                    break;

                case ExportTable table:
                    var snapshot = await ExportTableSnapshot
                        .CreateAsync(table, options, cancellationToken)
                        ;
                    blocks.Add(new PdfTableBlock(snapshot, options.IncludeHeaders));
                    break;

                case ExportChart chart:
                    blocks.Add(new PdfImageBlock(ChartPngRenderer.Render(chart.Chart), chart.Chart.Width));
                    break;

                case ExportImage image when !image.Data.IsEmpty:
                    blocks.Add(new PdfImageBlock(image.Data.ToArray(), image.Width));
                    break;

                case ExportPageBreak:
                    blocks.Add(new PdfPageBreakBlock());
                    break;

                default:
                    // Unknown section types are skipped so the model stays extensible.
                    break;
            }
        }

        return new PdfContent(blocks);
    }
}
