using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Csv;
using Albin.Export.Documents;
using Albin.Export.Excel;
using Albin.Export.Html;
using Albin.Export.Json;
using Albin.Export.Markdown;
using Albin.Export.Pdf;
using Albin.Export.Xml;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Albin.Export.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class ExportBenchmarks
{
    private IExportService _service = null!;
    private IReadOnlyList<SalesRow> _rows = null!;
    private ExportDocument _document = null!;
    private ChartSpec _chart = null!;

    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _service = new ExportService(
        [
            new CsvExporter(),
            new JsonExporter(),
            new XmlExporter(),
            new MarkdownExporter(),
            new HtmlExporter(),
            new ExcelExporter(),
            new PdfExporter(),
            new SvgExporter(),
            new PngExporter(),
        ]);

        _rows = CreateRows(RowCount);
        var table = ExportTable
            .Create(_rows, "Sales")
            .Column("Id", x => x.Id)
            .Column("Region", x => x.Region)
            .Column("Product", x => x.Product)
            .Column("Date", x => x.Date, format: "yyyy-MM-dd")
            .Column("Units", x => x.Units)
            .Column("Revenue", x => x.Revenue, format: "N2")
            .Build();

        _chart = new ChartSpec(
            ChartType.Bar,
            ["North", "South", "East", "West"],
            [ChartSeries.From("Revenue", [120d, 98d, 143.5d, 77d])])
        {
            Title = "Revenue",
        };

        _document = ExportDocument.Builder("Sales")
            .Paragraph("Prepared DTOs only.")
            .Table(table)
            .Chart(_chart)
            .Build();
    }

    [Benchmark]
    public Task Csv() => Export(ExportFormat.Csv);

    [Benchmark]
    public Task Json() => Export(ExportFormat.Json, ExportRequest.FromItems(_rows));

    [Benchmark]
    public Task Xml() => Export(ExportFormat.Xml);

    [Benchmark]
    public Task Markdown() => Export(ExportFormat.Markdown);

    [Benchmark]
    public Task Html() => Export(ExportFormat.Html);

    [Benchmark]
    public Task Excel() => Export(ExportFormat.Excel, options: new ExcelExportOptions { AutoFitColumns = false });

    [Benchmark]
    public Task Pdf() => Export(ExportFormat.Pdf);

    [Benchmark]
    public Task Svg() => Export(ExportFormat.Svg, new ExportRequest(ExportDocument.Builder("Chart").Chart(_chart).Build()));

    [Benchmark]
    public Task Png() => Export(ExportFormat.Png, new ExportRequest(ExportDocument.Builder("Chart").Chart(_chart).Build()));

    private async Task Export(ExportFormat format, ExportRequest? request = null, ExportOptions? options = null)
    {
        request ??= new ExportRequest(_document, options);
        await using var result = await _service.ExportAsync(request, format);
        _ = result.Length;
    }

    private static IReadOnlyList<SalesRow> CreateRows(int count)
    {
        var regions = new[] { "North", "South", "East", "West" };
        var products = new[] { "Widget", "Gadget", "Doohickey" };
        var rows = new SalesRow[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new SalesRow(
                i + 1,
                regions[i % regions.Length],
                products[i % products.Length],
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i % 365),
                (i % 40) + 1,
                100.5m + (i * 0.37m));
        }

        return rows;
    }

    public sealed record SalesRow(
        int Id,
        string Region,
        string Product,
        DateTime Date,
        int Units,
        decimal Revenue);
}
