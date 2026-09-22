using Albin.Export.Abstractions;
using Albin.Export.Documents;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Albin.Export.Pdf;

/// <summary>
/// Exports a document as PDF using PDFsharp and MigraDoc.
/// </summary>
/// <remarks>
/// <para>
/// MigraDoc handles pagination, so tables flow across pages with repeating header rows, and headings
/// stay with the content that follows them. Headers, footers with "Page X of Y", margins, fonts,
/// orientation and page size all come from <see cref="PdfExportOptions"/>. Images and
/// server-rendered charts are embedded from memory as base64 data, so no temporary files are created.
/// </para>
/// <para>
/// Fonts are resolved through <see cref="SystemFontResolver"/>, which is installed automatically the
/// first time an export runs. That is what makes the same code produce PDFs on Windows, Linux and in
/// a container; a slim container image still needs at least one font package installed
/// (for example <c>fonts-dejavu-core</c>).
/// </para>
/// </remarks>
public sealed class PdfExporter : IExporter
{
    private static readonly Color BorderColor = new(0xD0, 0xD7, 0xDE);
    private static readonly Color HeaderFill = new(0xF6, 0xF8, 0xFA);
    private static readonly Color StripeFill = new(0xFA, 0xFB, 0xFC);
    private static readonly Color MutedColor = new(0x57, 0x60, 0x6A);

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExporter"/> class and makes sure a font
    /// resolver is installed.
    /// </summary>
    public PdfExporter() => SystemFontResolver.Register();

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Pdf;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Pdf;

    /// <inheritdoc />
    public string FileExtension => ".pdf";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as PdfExportOptions ?? new PdfExportOptions();

        // MigraDoc is synchronous, so rows are read and formatted first.
        var blocks = await PdfContent.BuildAsync(request, options, cancellationToken);

        await ExportStreams.WriteSeekableAsync(
                destination,
                stream => Render(stream, request, options, blocks),
                capacityHint: 128 * 1024,
                cancellationToken)
            ;
    }

    private static void Render(Stream stream, ExportRequest request, PdfExportOptions options, PdfContent content)
    {
        var document = CreateDocument(request, options);
        var section = document.AddSection();
        ConfigurePage(section, options);
        AddHeaderAndFooter(section, request, options);

        if (!string.IsNullOrWhiteSpace(request.Document.Title))
        {
            var title = section.AddParagraph(request.Document.Title);
            title.Format.Font.Size = Unit.FromPoint(options.FontSize * 2d);
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = Unit.FromPoint(4d);
        }

        if (!string.IsNullOrWhiteSpace(request.Document.Subtitle))
        {
            var subtitle = section.AddParagraph(request.Document.Subtitle);
            subtitle.Format.Font.Color = MutedColor;
            subtitle.Format.SpaceAfter = Unit.FromPoint(14d);
        }

        var contentWidth = ContentWidth(options);
        foreach (var block in content.Blocks)
        {
            switch (block)
            {
                case PdfHeadingBlock heading:
                    AddHeading(section, heading, options);
                    break;
                case PdfParagraphBlock paragraph:
                    AddParagraph(section, paragraph, options);
                    break;
                case PdfTableBlock table:
                    AddTable(section, table.Table, table.IncludeHeaders, options, contentWidth);
                    break;
                case PdfImageBlock image:
                    AddImage(section, image, contentWidth);
                    break;
                case PdfPageBreakBlock:
                    section.AddPageBreak();
                    break;
            }
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(stream, closeStream: false);
    }

    private static Document CreateDocument(ExportRequest request, PdfExportOptions options)
    {
        var document = new Document();
        document.Info.Title = request.Document.Title ?? string.Empty;
        document.Info.Subject = request.Document.Subtitle ?? string.Empty;
        document.Info.Author = options.Author ?? string.Empty;

        var normal = document.Styles["Normal"];
        if (normal is not null)
        {
            normal.Font.Name = options.FontFamily;
            normal.Font.Size = Unit.FromPoint(options.FontSize);
            normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6d);
        }

        return document;
    }

    private static void ConfigurePage(Section section, PdfExportOptions options)
    {
        var setup = section.PageSetup;
        setup.PageFormat = options.PageSize switch
        {
            PdfPageSize.Letter => PageFormat.Letter,
            PdfPageSize.A3 => PageFormat.A3,
            PdfPageSize.A5 => PageFormat.A5,
            _ => PageFormat.A4,
        };

        setup.Orientation = options.Landscape ? Orientation.Landscape : Orientation.Portrait;
        setup.TopMargin = Unit.FromPoint(options.MarginTop);
        setup.BottomMargin = Unit.FromPoint(options.MarginBottom);
        setup.LeftMargin = Unit.FromPoint(options.MarginLeft);
        setup.RightMargin = Unit.FromPoint(options.MarginRight);
        setup.HeaderDistance = Unit.FromPoint(options.MarginTop / 2.5d);
        setup.FooterDistance = Unit.FromPoint(options.MarginBottom / 2.5d);
    }

    private static void AddHeaderAndFooter(Section section, ExportRequest request, PdfExportOptions options)
    {
        var headerText = options.HeaderText ?? request.Document.Title;
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            var header = section.Headers.Primary.AddParagraph(headerText);
            header.Format.Font.Size = Unit.FromPoint(options.FontSize * 0.85d);
            header.Format.Font.Color = MutedColor;
            header.Format.Borders.Bottom.Width = 0.5;
            header.Format.Borders.Bottom.Color = BorderColor;
            header.Format.SpaceAfter = Unit.FromPoint(6d);
        }

        var footerText = options.FooterText ?? request.Document.Footer;
        if (string.IsNullOrWhiteSpace(footerText) && !options.IncludePageNumbers)
        {
            return;
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = Unit.FromPoint(options.FontSize * 0.85d);
        footer.Format.Font.Color = MutedColor;
        footer.Format.AddTabStop(Unit.FromPoint(ContentWidth(options)), TabAlignment.Right);

        if (!string.IsNullOrWhiteSpace(footerText))
        {
            footer.AddText(footerText);
        }

        if (options.IncludePageNumbers)
        {
            footer.AddTab();
            footer.AddText("Page ");
            footer.AddPageField();
            footer.AddText(" of ");
            footer.AddNumPagesField();
        }
    }

    private static void AddHeading(Section section, PdfHeadingBlock block, PdfExportOptions options)
    {
        var scale = block.Level switch
        {
            1 => 1.6d,
            2 => 1.35d,
            3 => 1.15d,
            _ => 1.05d,
        };

        var paragraph = section.AddParagraph(block.Text);
        paragraph.Format.Font.Size = Unit.FromPoint(options.FontSize * scale);
        paragraph.Format.Font.Bold = true;
        paragraph.Format.SpaceBefore = Unit.FromPoint(12d);
        paragraph.Format.SpaceAfter = Unit.FromPoint(6d);
        paragraph.Format.KeepWithNext = true;
    }

    private static void AddParagraph(Section section, PdfParagraphBlock block, PdfExportOptions options)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.Font.Bold = block.Bold;
        if (block.Muted)
        {
            paragraph.Format.Font.Color = MutedColor;
        }

        var lines = block.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.AddLineBreak();
            }

            paragraph.AddText(lines[i]);
        }

        _ = options;
    }

    private static void AddTable(
        Section section,
        ExportTableSnapshot snapshot,
        bool includeHeaders,
        PdfExportOptions options,
        double contentWidth)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Name))
        {
            AddHeading(section, new PdfHeadingBlock(snapshot.Name, 3), options);
        }

        if (snapshot.ColumnCount == 0)
        {
            return;
        }

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = BorderColor;
        table.Format.Font.Size = Unit.FromPoint(options.FontSize * 0.95d);
        table.Format.SpaceAfter = Unit.FromPoint(12d);
        table.KeepTogether = false;

        var widths = snapshot.DistributeWidths(contentWidth);
        for (var i = 0; i < snapshot.ColumnCount; i++)
        {
            var column = table.AddColumn(Unit.FromPoint(widths[i]));
            column.Format.Alignment = ToAlignment(snapshot.Alignments[i]);
        }

        if (includeHeaders)
        {
            var headerRow = table.AddRow();
            if (snapshot.Rows.Count > 0)
            {
                // KeepWith/HeadingFormat require a following data row; empty tables would crash MigraDoc.
                headerRow.HeadingFormat = options.RepeatTableHeaders;
                headerRow.KeepWith = 1;
            }

            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = HeaderFill;

            for (var i = 0; i < snapshot.ColumnCount; i++)
            {
                headerRow.Cells[i].AddParagraph(snapshot.Headers[i]);
                headerRow.Cells[i].VerticalAlignment = VerticalAlignment.Center;
            }
        }

        for (var r = 0; r < snapshot.Rows.Count; r++)
        {
            var row = table.AddRow();
            if (options.StripedTables && r % 2 == 1)
            {
                row.Shading.Color = StripeFill;
            }

            var cells = snapshot.Rows[r];
            for (var i = 0; i < snapshot.ColumnCount; i++)
            {
                row.Cells[i].AddParagraph(i < cells.Length ? cells[i] : string.Empty);
            }
        }

        if (snapshot.Totals is { Count: > 0 } totals)
        {
            var totalsRow = table.AddRow();
            totalsRow.Format.Font.Bold = true;
            totalsRow.Borders.Top.Width = 1.0;
            totalsRow.Borders.Top.Color = BorderColor;

            for (var i = 0; i < snapshot.ColumnCount; i++)
            {
                totalsRow.Cells[i].AddParagraph(i < totals.Count ? totals[i] : string.Empty);
            }
        }

        if (snapshot.Rows.Count == 0)
        {
            var empty = section.AddParagraph("No data.");
            empty.Format.Font.Color = MutedColor;
            empty.Format.Font.Italic = true;
        }
    }

    private static void AddImage(Section section, PdfImageBlock block, double contentWidth)
    {
        // "base64:" keeps the bytes in memory; MigraDoc never touches the filesystem for this image.
        var image = section.AddImage("base64:" + Convert.ToBase64String(block.Data));
        image.LockAspectRatio = true;

        var width = block.Width is > 0 ? Math.Min(block.Width.Value, contentWidth) : contentWidth;
        image.Width = Unit.FromPoint(width);
        image.Left = ShapePosition.Left;
    }

    private static ParagraphAlignment ToAlignment(ExportAlignment alignment) => alignment switch
    {
        ExportAlignment.Right => ParagraphAlignment.Right,
        ExportAlignment.Center => ParagraphAlignment.Center,
        _ => ParagraphAlignment.Left,
    };

    private static double ContentWidth(PdfExportOptions options)
    {
        var (width, height) = options.PageSize switch
        {
            PdfPageSize.Letter => (612d, 792d),
            PdfPageSize.A3 => (841.89d, 1190.55d),
            PdfPageSize.A5 => (419.53d, 595.28d),
            _ => (595.28d, 841.89d),
        };

        var pageWidth = options.Landscape ? height : width;
        return Math.Max(pageWidth - options.MarginLeft - options.MarginRight, 72d);
    }
}
