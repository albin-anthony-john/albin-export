using System.Text;
using Albin.Export.Abstractions;
using Albin.Export.Csv;
using Albin.Export.Documents;

namespace Albin.Export.Tests;

public sealed class CsvExporterTests
{
    private readonly CsvExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Csv, _exporter.Format);
        Assert.Equal(".csv", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Csv, _exporter.ContentType);
    }

    [Fact]
    public async Task Writes_a_header_row_and_one_line_per_record()
    {
        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(3)));
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, lines.Length);
        Assert.Equal("Id,Region,Product,Date,Units,Revenue,Margin,Active", lines[0]);
        Assert.StartsWith("1,North,Widget,2026-01-01,", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Headers_can_be_suppressed()
    {
        var csv = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(2), new CsvExportOptions { IncludeHeaders = false }));

        Assert.DoesNotContain("Region", csv, StringComparison.Ordinal);
        Assert.Equal(2, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Quotes_fields_containing_delimiters_quotes_and_newlines()
    {
        var table = ExportTable.FromCells(
            ["A", "B"],
            [["has,comma", "has\"quote"], ["has\nnewline", "plain"]]);

        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(table));

        Assert.Contains("\"has,comma\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"has\"\"quote\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"has\nnewline\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Escapes_spreadsheet_formula_payloads_by_default()
    {
        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.InjectionTable()));

        Assert.DoesNotContain("\n=1+1", "\n" + csv, StringComparison.Ordinal);
        Assert.Contains("'=1+1", csv, StringComparison.Ordinal);
        Assert.Contains("'@SUM(1:9)", csv, StringComparison.Ordinal);
        Assert.Contains("'-2+3+cmd", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formula_escaping_can_be_turned_off_for_machine_consumers()
    {
        var csv = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.InjectionTable(), new CsvExportOptions { EscapeFormulas = false }));

        Assert.Contains("=1+1", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("'=1+1", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Honours_a_custom_delimiter_and_line_ending()
    {
        var csv = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(2), new CsvExportOptions { Delimiter = ";", NewLine = "\n" }));

        Assert.Contains("Id;Region;Product", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_a_byte_order_mark_by_default_for_excel()
    {
        var bytes = await TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesTable(1)));
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);

        var withoutBom = await TestData.RenderAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(1), new CsvExportOptions { WriteByteOrderMark = false }));
        Assert.NotEqual(0xEF, withoutBom[0]);
    }

    [Fact]
    public async Task Round_trips_unicode()
    {
        var bytes = await TestData.RenderAsync(_exporter, TestData.Request(TestData.UnicodeTable()));
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Contains("значение", csv, StringComparison.Ordinal);
        Assert.Contains("日本語のテキスト", csv, StringComparison.Ordinal);
        Assert.Contains("🚀🌍", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nulls_become_empty_fields()
    {
        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.NullTable()));
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("A,B,C", lines[0]);
        Assert.Equal(",,", lines[1]);
        Assert.Equal("x,,1", lines[2]);
    }

    [Fact]
    public async Task Null_text_is_configurable()
    {
        var csv = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.NullTable(), new CsvExportOptions { NullText = "NULL" }));

        Assert.Contains("NULL,NULL,NULL", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_dataset_still_writes_the_header()
    {
        var table = ExportTable.Empty(["A", "B"], "Empty");
        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(table));
        Assert.Equal("A,B\r\n", csv);
    }

    [Fact]
    public async Task A_document_without_tables_produces_no_rows()
    {
        var document = ExportDocument.Builder("Text only").Paragraph("Nothing tabular here.").Build();
        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(document));
        Assert.Equal(string.Empty, csv);
    }

    [Fact]
    public async Task Only_the_first_table_is_written_unless_asked_otherwise()
    {
        var document = new ExportDocument
        {
            Sections = [TestData.SalesTable(2), ExportTable.FromCells(["Other"], [["x"]], "Second")],
        };

        var single = await TestData.RenderTextAsync(_exporter, TestData.Request(document));
        Assert.DoesNotContain("Other", single, StringComparison.Ordinal);

        var both = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(document, new CsvExportOptions { IncludeAllTables = true }));
        Assert.Contains("Other", both, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_the_totals_row()
    {
        var table = ExportTable.Create(TestData.Sales(3), "Sales")
            .Column("Region", x => x.Region)
            .Column("Revenue", x => x.Revenue, format: "N2")
            .WithTotals("Total", 344.44m)
            .Build();

        var csv = await TestData.RenderTextAsync(_exporter, TestData.Request(table));
        Assert.EndsWith("Total,344.44\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handles_a_large_dataset_without_buffering_rows()
    {
        const int rows = 50_000;
        var table = ExportTable.Create(TestData.Sales(rows), "Large")
            .Column("Id", x => x.Id)
            .Column("Region", x => x.Region)
            .Column("Revenue", x => x.Revenue, format: "F2")
            .Build();

        using var destination = new MemoryStream();
        await _exporter.ExportAsync(TestData.Request(table), destination);

        var lines = TestData.StripBom(Encoding.UTF8.GetString(destination.ToArray()))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(rows + 1, lines.Length);
    }

    [Fact]
    public async Task Streams_to_a_non_seekable_destination()
    {
        using var inner = new MemoryStream();
        await using var destination = new WriteOnlyStream(inner);

        await _exporter.ExportAsync(TestData.Request(TestData.SalesTable(10)), destination);

        Assert.Contains("North", Encoding.UTF8.GetString(inner.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_dispose_the_destination()
    {
        using var destination = new MemoryStream();
        await _exporter.ExportAsync(TestData.Request(TestData.SalesTable(1)), destination);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesTable(1000)), cts.Token));
    }

    [Fact]
    public async Task Rejects_null_arguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(TestData.Request(TestData.SalesTable(1)), null!));
    }
}
