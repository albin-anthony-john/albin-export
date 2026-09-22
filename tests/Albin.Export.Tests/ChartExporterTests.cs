using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Documents;

namespace Albin.Export.Tests;

public sealed class ChartExporterTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Exporter_metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Svg, new SvgExporter().Format);
        Assert.Equal(".svg", new SvgExporter().FileExtension);
        Assert.Equal(ExportContentTypes.Svg, new SvgExporter().ContentType);

        Assert.Equal(ExportFormat.Png, new PngExporter().Format);
        Assert.Equal(".png", new PngExporter().FileExtension);
        Assert.Equal(ExportContentTypes.Png, new PngExporter().ContentType);

        Assert.Equal(ExportFormat.ECharts, new EChartsExporter().Format);
        Assert.Equal(".json", new EChartsExporter().FileExtension);
        Assert.Equal(ExportContentTypes.Json, new EChartsExporter().ContentType);
    }

    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    public void Svg_output_is_well_formed_for_every_chart_type(ChartType type)
    {
        var spec = new ChartSpec(type, ["North", "South", "East"], [new ChartSeries("2026", [1.5, -2, 30])])
        {
            Title = "Title",
            Subtitle = "Subtitle",
            XAxisLabel = "X",
            YAxisLabel = "Y",
            Width = 640,
            Height = 360,
        };

        var svg = ChartSvgRenderer.Render(spec);
        var document = XDocument.Parse(svg);

        Assert.Equal("svg", document.Root!.Name.LocalName);
        Assert.Equal("http://www.w3.org/2000/svg", document.Root.Name.NamespaceName);
        Assert.Equal("640", document.Root.Attribute("width")!.Value);
        Assert.Equal("0 0 640 360", document.Root.Attribute("viewBox")!.Value);
        Assert.Contains("Title", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Svg_escapes_labels_so_data_cannot_inject_markup()
    {
        var spec = new ChartSpec(
            ChartType.Bar,
            ["</text><script>alert(1)</script>"],
            [new ChartSeries("a & b <c>", [1])])
        {
            Title = "<script>title</script>",
        };

        var svg = ChartSvgRenderer.Render(spec);

        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", svg, StringComparison.Ordinal);
        Assert.Contains("&amp;", svg, StringComparison.Ordinal);
        XDocument.Parse(svg);
    }

    [Fact]
    public void Svg_uses_invariant_numbers_regardless_of_thread_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // German formats decimals with a comma, which would produce invalid SVG coordinates.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var svg = ChartSvgRenderer.Render(TestData.RevenueChart());

            XDocument.Parse(svg);
            Assert.DoesNotContain("143,5", svg, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    public void Png_output_is_a_valid_image_for_every_chart_type(ChartType type)
    {
        var spec = new ChartSpec(type, ["A", "B", "C"], [new ChartSeries("s", [1, 2, 3])])
        {
            Title = "Title",
            Width = 320,
            Height = 240,
        };

        var png = ChartPngRenderer.Render(spec);

        Assert.Equal(PngSignature, png[..8]);
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(320, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(240, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(8, png[24]);   // bit depth
        Assert.Equal(6, png[25]);   // colour type: RGBA
        Assert.Equal("IEND", Encoding.ASCII.GetString(png, png.Length - 8, 4));
        Assert.Contains("IDAT", Encoding.Latin1.GetString(png), StringComparison.Ordinal);
    }

    [Fact]
    public void Png_rendering_is_deterministic()
    {
        var first = ChartPngRenderer.Render(TestData.RevenueChart());
        var second = ChartPngRenderer.Render(TestData.RevenueChart());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Echarts_option_matches_the_spec()
    {
        var json = EChartsOptionBuilder.ToJson(TestData.RevenueChart());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("animation").GetBoolean());
        Assert.Equal("Revenue by region", root.GetProperty("title").GetProperty("text").GetString());
        Assert.Equal("in thousands", root.GetProperty("title").GetProperty("subtext").GetString());
        Assert.Equal("category", root.GetProperty("xAxis").GetProperty("type").GetString());
        Assert.Equal("North", root.GetProperty("xAxis").GetProperty("data")[0].GetString());
        Assert.Equal("Revenue", root.GetProperty("yAxis").GetProperty("name").GetString());

        var series = root.GetProperty("series");
        Assert.Equal(2, series.GetArrayLength());
        Assert.Equal("bar", series[0].GetProperty("type").GetString());
        Assert.Equal("2025", series[0].GetProperty("name").GetString());
        Assert.Equal(120, series[0].GetProperty("data")[0].GetDouble());
        Assert.Equal(JsonValueKind.Null, series[1].GetProperty("data")[1].ValueKind);
    }

    [Fact]
    public void Echarts_maps_each_chart_type()
    {
        using var line = JsonDocument.Parse(
            EChartsOptionBuilder.ToJson(new ChartSpec(ChartType.Line, ["A"], [new ChartSeries("s", [1])])));
        Assert.Equal("line", line.RootElement.GetProperty("series")[0].GetProperty("type").GetString());

        using var area = JsonDocument.Parse(
            EChartsOptionBuilder.ToJson(new ChartSpec(ChartType.Area, ["A"], [new ChartSeries("s", [1])])));
        Assert.Equal("line", area.RootElement.GetProperty("series")[0].GetProperty("type").GetString());
        Assert.True(area.RootElement.GetProperty("series")[0].TryGetProperty("areaStyle", out _));

        using var pie = JsonDocument.Parse(
            EChartsOptionBuilder.ToJson(new ChartSpec(ChartType.Pie, ["A", "B"], [new ChartSeries("s", [1, 2])])));
        var pieSeries = pie.RootElement.GetProperty("series")[0];
        Assert.Equal("pie", pieSeries.GetProperty("type").GetString());
        Assert.Equal("A", pieSeries.GetProperty("data")[0].GetProperty("name").GetString());
        Assert.Equal("item", pie.RootElement.GetProperty("tooltip").GetProperty("trigger").GetString());
        Assert.False(pie.RootElement.TryGetProperty("xAxis", out _));
    }

    [Fact]
    public void Echarts_escapes_html_sensitive_text()
    {
        var spec = new ChartSpec(ChartType.Bar, ["<b>x</b>"], [new ChartSeries("s", [1])])
        {
            Title = "<script>alert(1)</script>",
        };

        var json = EChartsOptionBuilder.ToJson(spec);

        Assert.DoesNotContain("<script>", json, StringComparison.Ordinal);
        Assert.Contains("\\u003C", json, StringComparison.Ordinal);
        JsonDocument.Parse(json).Dispose();
    }

    [Fact]
    public async Task Exporters_use_the_chart_from_options_first()
    {
        var options = new ChartExportOptions { Chart = TestData.RevenueChart() };
        var svg = await TestData.RenderTextAsync(
            new SvgExporter(),
            TestData.Request(TestData.SalesTable(3), options));

        Assert.Contains("Revenue by region", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exporters_use_a_chart_section_when_present()
    {
        var document = ExportDocument.Builder("Doc").Chart(TestData.RevenueChart()).Build();
        var svg = await TestData.RenderTextAsync(new SvgExporter(), TestData.Request(document));
        Assert.Contains("Revenue by region", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exporters_derive_a_chart_from_the_primary_table()
    {
        var table = ExportTable.Create(TestData.Sales(4), "Revenue by region")
            .Column("Region", x => x.Region)
            .Column("Units", x => x.Units)
            .Column("Revenue", x => x.Revenue)
            .Build();

        var json = await TestData.RenderTextAsync(new EChartsExporter(), TestData.Request(table));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Revenue by region", document.RootElement.GetProperty("title").GetProperty("text").GetString());
        Assert.Equal("North", document.RootElement.GetProperty("xAxis").GetProperty("data")[0].GetString());
        Assert.Equal(2, document.RootElement.GetProperty("series").GetArrayLength());
    }

    [Fact]
    public async Task Derived_charts_are_capped_to_keep_output_readable()
    {
        var table = ExportTable.Create(TestData.Sales(500), "Large")
            .Column("Label", x => x.Id.ToString(CultureInfo.InvariantCulture))
            .Column("Revenue", x => x.Revenue)
            .Build();

        var options = new ChartExportOptions { MaxDerivedCategories = 10 };
        var json = await TestData.RenderTextAsync(new EChartsExporter(), TestData.Request(table, options));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(10, document.RootElement.GetProperty("xAxis").GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task A_request_without_chartable_data_fails_with_a_clear_message()
    {
        var textOnly = ExportTable.FromCells(["A", "B"], [["x", "y"]], "Text only");
        var noNumbers = await Assert.ThrowsAsync<ExportException>(
            () => TestData.RenderAsync(new SvgExporter(), TestData.Request(textOnly)));
        Assert.Contains("no numeric", noNumbers.Message, StringComparison.OrdinalIgnoreCase);

        var prose = ExportDocument.Builder("Prose").Paragraph("nothing to chart").Build();
        var noChart = await Assert.ThrowsAsync<ExportException>(
            () => TestData.RenderAsync(new PngExporter(), TestData.Request(prose)));
        Assert.Contains("no chart", noChart.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Png_exporter_streams_to_a_non_seekable_destination()
    {
        var document = ExportDocument.Builder("Chart").Chart(TestData.RevenueChart()).Build();

        using var inner = new MemoryStream();
        await using (var destination = new WriteOnlyStream(inner))
        {
            await new PngExporter().ExportAsync(TestData.Request(document), destination);
        }

        Assert.Equal(PngSignature, inner.ToArray()[..8]);
    }

    [Fact]
    public void Charts_reject_invalid_input()
    {
        Assert.Throws<ArgumentNullException>(() => new ChartSpec(ChartType.Bar, null!, [new ChartSeries("s", [1])]));
        Assert.Throws<ArgumentNullException>(() => new ChartSpec(ChartType.Bar, [], null!));
        Assert.Throws<ArgumentException>(() => new ChartSpec(ChartType.Bar, [], []));
        Assert.Throws<ArgumentNullException>(() => new ChartSeries(null!, [1]));
        Assert.Throws<ArgumentException>(() => new ChartSeries("  ", [1]));
        Assert.Throws<ArgumentNullException>(() => new ChartSeries("s", null!));
        Assert.Throws<ArgumentNullException>(() => ChartSvgRenderer.Render(null!));
        Assert.Throws<ArgumentNullException>(() => ChartPngRenderer.Render(null!));
        Assert.Throws<ArgumentNullException>(() => EChartsOptionBuilder.Build(null!));
    }

    [Fact]
    public void Series_colours_fall_back_to_the_palette_and_wrap_around()
    {
        var spec = new ChartSpec(
            ChartType.Bar,
            ["A"],
            [new ChartSeries("explicit", [1]) { Color = "#123456" }, new ChartSeries("default", [2])]);

        Assert.Equal("#123456", spec.ColorFor(0));
        Assert.Equal(ChartSpec.DefaultPalette[1], spec.ColorFor(1));
        Assert.Equal(ChartSpec.DefaultPalette[0], spec.ColorFor(ChartSpec.DefaultPalette.Count));
    }

    [Fact]
    public async Task Chart_exporters_honour_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = TestData.Request(TestData.SalesTable(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(new SvgExporter(), request, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(new PngExporter(), request, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(new EChartsExporter(), request, cts.Token));
    }
}
