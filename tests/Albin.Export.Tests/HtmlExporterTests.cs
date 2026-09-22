using System.Xml.Linq;
using Albin.Export.Abstractions;
using Albin.Export.Documents;
using Albin.Export.Html;

namespace Albin.Export.Tests;

public sealed class HtmlExporterTests
{
    private readonly HtmlExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Html, _exporter.Format);
        Assert.Equal(".html", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Html, _exporter.ContentType);
    }

    [Fact]
    public async Task Produces_a_complete_responsive_print_friendly_document()
    {
        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesDocument(3)));

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("name=\"viewport\"", html, StringComparison.Ordinal);
        Assert.Contains("@media print", html, StringComparison.Ordinal);
        Assert.Contains("<title>Quarterly sales</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"export-title\">Quarterly sales</h1>", html, StringComparison.Ordinal);
        Assert.Contains("</html>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encodes_data_so_untrusted_content_cannot_inject_markup()
    {
        var table = ExportTable.FromCells(
            ["<b>Header</b>", "Payload"],
            [
                ["<script>alert('xss')</script>", "\" onmouseover=\"alert(1)"],
                ["<img src=x onerror=alert(1)>", "javascript:alert(1)"],
            ],
            "<i>Table</i>");

        var document = ExportDocument.Builder("<script>title</script>")
            .Paragraph("<script>paragraph</script>")
            .Heading("<script>heading</script>")
            .Table(table)
            .Build();

        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(document));

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_a_table_with_aligned_cells_and_striping()
    {
        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(3)));

        Assert.Contains("<caption>Sales</caption>", html, StringComparison.Ordinal);
        Assert.Contains("<th class=\"num\">Id</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Region</th>", html, StringComparison.Ordinal);
        Assert.Contains("<tr class=\"stripe\">", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"num\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Striping_can_be_disabled()
    {
        var html = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(4), new HtmlExportOptions { StripedTables = false }));

        Assert.DoesNotContain("<tr class=\"stripe\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fragment_mode_omits_the_document_shell()
    {
        var html = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(1), new HtmlExportOptions { Fragment = true }));

        Assert.DoesNotContain("<!DOCTYPE", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", html, StringComparison.Ordinal);
        Assert.StartsWith("<div class=\"export\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renders_charts_as_inline_svg_by_default()
    {
        var document = ExportDocument.Builder("Chart").Chart(TestData.RevenueChart()).Build();
        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(document));

        Assert.Contains("<svg xmlns=\"http://www.w3.org/2000/svg\"", html, StringComparison.Ordinal);
        Assert.Contains("<figcaption", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-echarts-option", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_side_echarts_mode_emits_data_and_a_fixed_loader()
    {
        var document = ExportDocument.Builder("Chart").Chart(TestData.RevenueChart()).Build();
        var options = new HtmlExportOptions
        {
            ChartMode = HtmlChartMode.EChartsClient,
            EChartsScriptUrl = "https://cdn.example.com/echarts.min.js",
        };

        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(document, options));

        Assert.Contains("data-echarts-option=\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"albin-chart-1\"", html, StringComparison.Ordinal);
        Assert.Contains("https://cdn.example.com/echarts.min.js", html, StringComparison.Ordinal);
        Assert.Contains("JSON.parse(el.dataset.echartsOption)", html, StringComparison.Ordinal);

        // The option payload is HTML-encoded inside the attribute, so quotes cannot break out of it.
        Assert.DoesNotContain("data-echarts-option=\"{\"", html, StringComparison.Ordinal);
        Assert.Contains("&quot;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Charts_can_be_suppressed()
    {
        var document = ExportDocument.Builder("Chart").Chart(TestData.RevenueChart()).Build();
        var html = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(document, new HtmlExportOptions { ChartMode = HtmlChartMode.None }));

        Assert.DoesNotContain("<svg", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_table_reports_no_data()
    {
        var html = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(ExportTable.Empty(["A", "B"], "Empty")));

        Assert.Contains("class=\"empty\"", html, StringComparison.Ordinal);
        Assert.Contains("No data.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_a_totals_footer()
    {
        var table = ExportTable.Create(TestData.Sales(2), "Sales")
            .Column("Region", x => x.Region)
            .Column("Revenue", x => x.Revenue, format: "N2")
            .WithTotals("Total", 214.37m)
            .Build();

        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(table));

        Assert.Contains("<tfoot><tr>", html, StringComparison.Ordinal);
        Assert.Contains("214.37", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Additional_css_is_appended()
    {
        var html = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(1), new HtmlExportOptions { AdditionalCss = ".x { color: red; }" }));

        Assert.Contains(".x { color: red; }", html, StringComparison.Ordinal);
        Assert.Contains(HtmlStyles.Default[..40], html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_body_fragment_is_well_formed_markup()
    {
        var options = new HtmlExportOptions { Fragment = true, ChartMode = HtmlChartMode.None };
        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesDocument(3), options));

        // Parsing as XML is a strict well-formedness check (all tags closed, all attributes quoted).
        var parsed = XDocument.Parse(html.Replace("<br>", "<br/>", StringComparison.Ordinal));
        Assert.Equal("div", parsed.Root!.Name.LocalName);
    }

    [Fact]
    public async Task Non_ascii_text_is_escaped_to_numeric_entities_and_decodes_back()
    {
        var html = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.UnicodeTable()));

        // HtmlEncoder.Default escapes everything outside Basic Latin, which keeps a fragment safe
        // even when the host page declares a different charset.
        Assert.DoesNotContain("日本語のテキスト", html, StringComparison.Ordinal);
        Assert.Contains("&#x65E5;", html, StringComparison.Ordinal);

        var decoded = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("日本語のテキスト", decoded, StringComparison.Ordinal);
        Assert.Contains("🚀🌍", decoded, StringComparison.Ordinal);
        Assert.Contains("значение", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Streams_to_a_non_seekable_destination()
    {
        using var inner = new MemoryStream();
        await using var destination = new WriteOnlyStream(inner);

        await _exporter.ExportAsync(TestData.Request(TestData.SalesTable(50)), destination);

        Assert.True(inner.Length > 0);
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesTable(500)), cts.Token));
    }
}
