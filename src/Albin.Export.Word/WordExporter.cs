using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Albin.Export.Word;

/// <summary>
/// Exports a document as an Office Open XML word processing document (<c>.docx</c>) using the Open XML SDK.
/// </summary>
/// <remarks>
/// Headings carry an outline level so Word's navigation pane works, table header rows repeat across
/// page breaks, and images (including server-rendered charts) are embedded as inline pictures. The
/// package is assembled through <see cref="ExportStreams"/>, which writes straight
/// into the caller's buffer when it is seekable and only falls back to an intermediate buffer for
/// non-seekable destinations such as a response body.
/// </remarks>
public sealed class WordExporter : IExporter
{
    private const int EmuPerPixel = 9525;
    private const string HeaderShade = "F6F8FA";
    private const string BorderColor = "D0D7DE";

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Word;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Word;

    /// <inheritdoc />
    public string FileExtension => ".docx";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as WordExportOptions ?? new WordExportOptions();

        // Rows are pulled asynchronously first: the Open XML object model is synchronous, so the
        // document tree is assembled from already-materialized content.
        var content = await WordContent.BuildAsync(request, options, cancellationToken);

        await ExportStreams.WriteSeekableAsync(
                destination,
                stream => Write(stream, request, options, content),
                capacityHint: 64 * 1024,
                cancellationToken)
            ;
    }

    private static void Write(Stream stream, ExportRequest request, WordExportOptions options, WordContent content)
    {
        using var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = package.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);

        SetMetadata(package, request, options);
        AppendStyles(mainPart, options);

        if (!string.IsNullOrWhiteSpace(request.Document.Title))
        {
            body.AppendChild(CreateHeading(request.Document.Title, 1, options));
        }

        if (!string.IsNullOrWhiteSpace(request.Document.Subtitle))
        {
            body.AppendChild(CreateParagraph(request.Document.Subtitle, options, muted: true));
        }

        foreach (var block in content.Blocks)
        {
            switch (block)
            {
                case WordHeadingBlock heading:
                    body.AppendChild(CreateHeading(heading.Text, heading.Level, options));
                    break;
                case WordParagraphBlock paragraph:
                    body.AppendChild(CreateParagraph(paragraph.Text, options, paragraph.Bold, paragraph.Muted));
                    break;
                case WordTableBlock table:
                    AppendTable(body, table, options);
                    break;
                case WordImageBlock image:
                    AppendImage(mainPart, body, image);
                    break;
                case WordPageBreakBlock:
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                    break;
            }
        }

        body.AppendChild(CreateSectionProperties(mainPart, request, options));
        mainPart.Document.Save();
    }

    private static void SetMetadata(WordprocessingDocument package, ExportRequest request, WordExportOptions options)
    {
        var properties = package.PackageProperties;
        properties.Title = request.Document.Title;
        properties.Subject = request.Document.Subtitle;
        properties.Creator = options.Author;
        properties.Created = DateTime.UtcNow;
    }

    private static void AppendStyles(MainDocumentPart mainPart, WordExportOptions options)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var halfPoints = Math.Max((int)Math.Round(options.FontSize * 2d), 2).ToString(options.Culture);

        stylesPart.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        new RunFonts { Ascii = options.FontFamily, HighAnsi = options.FontFamily },
                        new FontSize { Val = halfPoints })),
                new ParagraphPropertiesDefault(
                    new ParagraphPropertiesBaseStyle(
                        new SpacingBetweenLines { After = "120", Line = "260", LineRule = LineSpacingRuleValues.Auto }))));
        stylesPart.Styles.Save();
    }

    private static Paragraph CreateHeading(string text, int level, WordExportOptions options)
    {
        var size = level switch
        {
            1 => options.FontSize * 1.9d,
            2 => options.FontSize * 1.5d,
            3 => options.FontSize * 1.25d,
            _ => options.FontSize * 1.1d,
        };

        return new Paragraph(
            new ParagraphProperties(
                new OutlineLevel { Val = Math.Clamp(level - 1, 0, 8) },
                new KeepNext(),
                new SpacingBetweenLines { Before = "240", After = "120" }),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = ((int)Math.Round(size * 2d)).ToString(options.Culture) }),
                CreateText(text)));
    }

    private static Paragraph CreateParagraph(string text, WordExportOptions options, bool bold = false, bool muted = false)
    {
        var runProperties = new RunProperties();
        if (bold)
        {
            runProperties.AppendChild(new Bold());
        }

        if (muted)
        {
            runProperties.AppendChild(new Color { Val = "57606A" });
        }

        var run = new Run(runProperties);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                run.AppendChild(new Break());
            }

            run.AppendChild(CreateText(lines[i]));
        }

        return new Paragraph(run);
    }

    private static void AppendTable(Body body, WordTableBlock block, WordExportOptions options)
    {
        var snapshot = block.Table;
        if (!string.IsNullOrWhiteSpace(snapshot.Name))
        {
            body.AppendChild(CreateHeading(snapshot.Name, 3, options));
        }

        var properties = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Autofit });

        if (options.StyleTables)
        {
            properties.AppendChild(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor }));
        }

        var table = new Table(properties);

        if (block.IncludeHeaders && snapshot.ColumnCount > 0)
        {
            var headerRow = new TableRow(new TableRowProperties(new TableHeader()));
            for (var i = 0; i < snapshot.ColumnCount; i++)
            {
                headerRow.AppendChild(CreateCell(snapshot.Headers[i], snapshot.Alignments[i], options, header: true));
            }

            table.AppendChild(headerRow);
        }

        foreach (var row in snapshot.Rows)
        {
            var tableRow = new TableRow();
            for (var i = 0; i < row.Length; i++)
            {
                tableRow.AppendChild(CreateCell(row[i], snapshot.Alignments[i], options, header: false));
            }

            table.AppendChild(tableRow);
        }

        if (snapshot.Totals is { Count: > 0 } totals)
        {
            var totalsRow = new TableRow();
            for (var i = 0; i < totals.Count; i++)
            {
                totalsRow.AppendChild(CreateCell(totals[i], snapshot.Alignments[i], options, header: false, bold: true));
            }

            table.AppendChild(totalsRow);
        }

        body.AppendChild(table);

        if (snapshot.Rows.Count == 0)
        {
            body.AppendChild(CreateParagraph("No data.", options, muted: true));
        }
        else
        {
            body.AppendChild(new Paragraph());
        }
    }

    private static TableCell CreateCell(
        string text,
        ExportAlignment alignment,
        WordExportOptions options,
        bool header,
        bool bold = false)
    {
        var cellProperties = new TableCellProperties(
            new TableCellMargin(
                new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));

        if (header && options.StyleTables)
        {
            cellProperties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderShade });
        }

        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { After = "0", Before = "0" });

        var justification = alignment switch
        {
            ExportAlignment.Right => JustificationValues.Right,
            ExportAlignment.Center => JustificationValues.Center,
            _ => JustificationValues.Left,
        };
        paragraphProperties.AppendChild(new Justification { Val = justification });

        var runProperties = new RunProperties();
        if (header || bold)
        {
            runProperties.AppendChild(new Bold());
        }

        return new TableCell(
            cellProperties,
            new Paragraph(paragraphProperties, new Run(runProperties, CreateText(text))));
    }

    private static void AppendImage(MainDocumentPart mainPart, Body body, WordImageBlock block)
    {
        var partType = block.ContentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
            ? ImagePartType.Jpeg
            : ImagePartType.Png;

        var imagePart = mainPart.AddImagePart(partType);
        using (var source = new MemoryStream(block.Data, writable: false))
        {
            imagePart.FeedData(source);
        }

        var relationshipId = mainPart.GetIdOfPart(imagePart);
        if (!ImageInfo.TryGetSize(block.Data, out var pixelWidth, out var pixelHeight))
        {
            pixelWidth = 600;
            pixelHeight = 320;
        }

        var (width, height) = Scale(pixelWidth, pixelHeight, block.Width, block.Height);
        var cx = (long)width * EmuPerPixel;
        var cy = (long)height * EmuPerPixel;
        var name = block.AltText ?? "Image";

        var drawing = new Drawing(
            new WP.Inline(
                new WP.Extent { Cx = cx, Cy = cy },
                new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties { Id = 1U, Name = name, Description = name },
                new WP.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                    }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });

        body.AppendChild(new Paragraph(new Run(drawing)));
    }

    private static (int Width, int Height) Scale(int pixelWidth, int pixelHeight, double? requestedWidth, double? requestedHeight)
    {
        const int maxWidth = 620;

        if (requestedWidth is > 0 && requestedHeight is > 0)
        {
            return ((int)requestedWidth.Value, (int)requestedHeight.Value);
        }

        if (requestedWidth is > 0)
        {
            var scale = requestedWidth.Value / pixelWidth;
            return ((int)requestedWidth.Value, Math.Max((int)(pixelHeight * scale), 1));
        }

        if (requestedHeight is > 0)
        {
            var scale = requestedHeight.Value / pixelHeight;
            return (Math.Max((int)(pixelWidth * scale), 1), (int)requestedHeight.Value);
        }

        if (pixelWidth <= maxWidth)
        {
            return (pixelWidth, pixelHeight);
        }

        var fit = (double)maxWidth / pixelWidth;
        return (maxWidth, Math.Max((int)(pixelHeight * fit), 1));
    }

    private static SectionProperties CreateSectionProperties(
        MainDocumentPart mainPart,
        ExportRequest request,
        WordExportOptions options)
    {
        var (widthTwips, heightTwips) = PageDimensions(options);
        var margin = (uint)Math.Max(options.Margin * 20d, 0d);

        var sectionProperties = new SectionProperties();

        var headerText = options.HeaderText ?? request.Document.Title;
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new Header(
                new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                    new Run(new RunProperties(new Color { Val = "57606A" }), CreateText(headerText))));
            headerPart.Header.Save();

            sectionProperties.AppendChild(new HeaderReference
            {
                Type = HeaderFooterValues.Default,
                Id = mainPart.GetIdOfPart(headerPart),
            });
        }

        var footerText = options.FooterText ?? request.Document.Footer;
        if (!string.IsNullOrWhiteSpace(footerText) || options.IncludePageNumbers)
        {
            var footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(CreateFooterParagraph(footerText, options));
            footerPart.Footer.Save();

            sectionProperties.AppendChild(new FooterReference
            {
                Type = HeaderFooterValues.Default,
                Id = mainPart.GetIdOfPart(footerPart),
            });
        }

        sectionProperties.AppendChild(new PageSize
        {
            Width = (uint)widthTwips,
            Height = (uint)heightTwips,
            Orient = options.Landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait,
        });

        sectionProperties.AppendChild(new PageMargin
        {
            Top = (int)margin,
            Bottom = (int)margin,
            Left = margin,
            Right = margin,
            Header = (uint)(margin / 2),
            Footer = (uint)(margin / 2),
        });

        return sectionProperties;
    }

    private static Paragraph CreateFooterParagraph(string? footerText, WordExportOptions options)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification
                {
                    Val = options.IncludePageNumbers ? JustificationValues.Both : JustificationValues.Left,
                }));

        var runProperties = () => new RunProperties(new Color { Val = "57606A" });

        if (!string.IsNullOrWhiteSpace(footerText))
        {
            paragraph.AppendChild(new Run(runProperties(), CreateText(footerText)));
        }

        if (options.IncludePageNumbers)
        {
            paragraph.AppendChild(new Run(runProperties(), CreateText("\tPage ")));
            paragraph.AppendChild(new SimpleField { Instruction = "PAGE" });
            paragraph.AppendChild(new Run(runProperties(), CreateText(" of ")));
            paragraph.AppendChild(new SimpleField { Instruction = "NUMPAGES" });
        }

        return paragraph;
    }

    private static (int Width, int Height) PageDimensions(WordExportOptions options)
    {
        var (width, height) = options.PageSize switch
        {
            ExportPageSize.Letter => (12240, 15840),
            ExportPageSize.A3 => (16838, 23811),
            _ => (11906, 16838),
        };

        return options.Landscape ? (height, width) : (width, height);
    }

    private static Text CreateText(string? value)
        => new(value ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve };
}
