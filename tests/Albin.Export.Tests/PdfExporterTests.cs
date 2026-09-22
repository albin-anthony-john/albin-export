using System.Text;
using Albin.Export.Abstractions;
using Albin.Export.Documents;
using Albin.Export.Pdf;
using PdfSharp.Pdf.IO;

namespace Albin.Export.Tests;

public sealed class PdfExporterTests
{
    private readonly PdfExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Pdf, _exporter.Format);
        Assert.Equal(".pdf", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Pdf, _exporter.ContentType);
    }

    [Fact]
    public void The_font_resolver_finds_fonts_on_this_host()
    {
        Assert.True(
            SystemFontResolver.Instance.HasFonts,
            "No TrueType or OpenType font was found. PDF export needs at least one installed font.");
        Assert.True(SystemFontResolver.Instance.FaceCount > 0);
        Assert.NotNull(SystemFontResolver.Instance.ResolveTypeface("Arial", false, false));
        Assert.NotNull(SystemFontResolver.Instance.ResolveTypeface("A Font That Does Not Exist", true, true));
    }

    [Fact]
    public async Task Produces_a_valid_pdf()
    {
        var bytes = await TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesDocument(20)));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes[..5]));
        Assert.True(bytes.Length > 1500, $"Unexpectedly small PDF: {bytes.Length} bytes.");
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes[^32..]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sets_document_metadata_and_page_geometry()
    {
        var options = new PdfExportOptions { Author = "Albin", PageSize = PdfPageSize.A4 };
        using var document = await OpenAsync(TestData.Request(TestData.SalesDocument(5), options));

        Assert.Equal("Quarterly sales", document.Info.Title);
        Assert.Equal("Albin", document.Info.Author);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(595, Math.Round(document.Pages[0].Width.Point));
        Assert.Equal(842, Math.Round(document.Pages[0].Height.Point));
    }

    [Fact]
    public async Task Landscape_and_page_size_options_are_applied()
    {
        var options = new PdfExportOptions { PageSize = PdfPageSize.Letter, Landscape = true };
        using var document = await OpenAsync(TestData.Request(TestData.SalesTable(3), options));

        Assert.Equal(792, Math.Round(document.Pages[0].Width.Point));
        Assert.Equal(612, Math.Round(document.Pages[0].Height.Point));
    }

    [Fact]
    public async Task Long_tables_paginate()
    {
        using var document = await OpenAsync(TestData.Request(TestData.SalesTable(400)));
        Assert.True(document.PageCount > 5, $"Expected several pages, got {document.PageCount}.");
    }

    [Fact]
    public async Task Explicit_page_breaks_create_pages()
    {
        var document = ExportDocument.Builder("Paged")
            .Paragraph("page one")
            .PageBreak()
            .Paragraph("page two")
            .PageBreak()
            .Paragraph("page three")
            .Build();

        using var pdf = await OpenAsync(TestData.Request(document));
        Assert.Equal(3, pdf.PageCount);
    }

    [Fact]
    public async Task Embeds_charts_and_images()
    {
        var document = ExportDocument.Builder("With chart").Chart(TestData.RevenueChart()).Build();
        var withChart = await TestData.RenderAsync(_exporter, TestData.Request(document));
        var withoutChart = await TestData.RenderAsync(
            _exporter,
            TestData.Request(ExportDocument.Builder("Plain").Paragraph("text").Build()));

        Assert.True(withChart.Length > withoutChart.Length + 2000);
    }

    [Fact]
    public async Task Handles_unicode_nulls_and_empty_tables()
    {
        var unicode = await TestData.RenderAsync(_exporter, TestData.Request(TestData.UnicodeTable()));
        Assert.True(unicode.Length > 1000);

        var nulls = await TestData.RenderAsync(_exporter, TestData.Request(TestData.NullTable()));
        Assert.True(nulls.Length > 1000);

        var empty = await TestData.RenderAsync(
            _exporter,
            TestData.Request(ExportTable.Empty(["A", "B"], "Empty")));
        Assert.True(empty.Length > 500);
    }

    [Fact]
    public async Task A_document_with_only_text_still_renders()
    {
        var document = ExportDocument.Builder("Text only")
            .Heading("Chapter", 2)
            .Paragraph("First line\nSecond line")
            .Paragraph("Bold", bold: true)
            .Paragraph("Muted", muted: true)
            .Build();

        using var pdf = await OpenAsync(TestData.Request(document));
        Assert.Equal(1, pdf.PageCount);
    }

    [Fact]
    public async Task Writes_to_a_non_seekable_destination()
    {
        using var inner = new MemoryStream();
        await using (var destination = new WriteOnlyStream(inner))
        {
            await _exporter.ExportAsync(TestData.Request(TestData.SalesTable(30)), destination);
        }

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(inner.ToArray()[..5]));
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesTable(100)), cts.Token));
    }

    [Fact]
    public async Task Rejects_null_arguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _exporter.ExportAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(TestData.Request(TestData.SalesTable(1)), null!));
    }

    private async Task<PdfSharp.Pdf.PdfDocument> OpenAsync(ExportRequest request)
    {
        var bytes = await TestData.RenderAsync(_exporter, request);
        return PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
    }
}
