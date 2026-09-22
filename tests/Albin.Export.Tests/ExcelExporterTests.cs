using Albin.Export.Abstractions;
using Albin.Export.Documents;
using Albin.Export.Excel;
using ClosedXML.Excel;

namespace Albin.Export.Tests;

public sealed class ExcelExporterTests
{
    private readonly ExcelExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Excel, _exporter.Format);
        Assert.Equal(".xlsx", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Excel, _exporter.ContentType);
    }

    [Fact]
    public async Task Writes_a_worksheet_with_typed_cells()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesTable(3)));
        var sheet = workbook.Worksheet("Sales");

        Assert.Equal("Id", sheet.Cell(1, 1).GetString());
        Assert.Equal("North", sheet.Cell(2, 2).GetString());
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 1).DataType);
        Assert.Equal(XLDataType.DateTime, sheet.Cell(2, 4).DataType);
        Assert.Equal(XLDataType.Boolean, sheet.Cell(2, 8).DataType);
        Assert.Equal(1, sheet.Cell(2, 1).GetValue<int>());
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Styles_freezes_and_filters_the_header_row()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesTable(5)));
        var sheet = workbook.Worksheet("Sales");

        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
        Assert.Equal(1, sheet.SheetView.SplitRow);
        Assert.True(sheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public async Task Header_styling_and_auto_fit_can_be_disabled()
    {
        var options = new ExcelExportOptions { StyleHeaderRow = false, AutoFitColumns = false };
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesTable(2), options));
        Assert.False(workbook.Worksheet("Sales").Cell(1, 1).Style.Font.Bold);
    }

    [Fact]
    public async Task Applies_excel_number_formats_translated_from_dotnet_formats()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesTable(2)));
        var sheet = workbook.Worksheet("Sales");

        Assert.Equal("#,##0.00", sheet.Cell(2, 6).Style.NumberFormat.Format);
        Assert.Equal("yyyy-mm-dd", sheet.Cell(2, 4).Style.NumberFormat.Format);
        Assert.Equal("0.0%", sheet.Cell(2, 7).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task Excel_custom_formats_pass_through_unchanged()
    {
        var table = ExportTable.Create(TestData.Sales(1), "Formats")
            .Column("Amount", x => x.Revenue, format: "#,##0.000")
            .Build();

        using var workbook = await ExportWorkbookAsync(TestData.Request(table));
        Assert.Equal("#,##0.000", workbook.Worksheet("Formats").Cell(2, 1).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task One_worksheet_per_table_with_sanitized_unique_names()
    {
        var document = new ExportDocument
        {
            Sections =
            [
                ExportTable.FromCells(["A"], [["1"]], "Report[2026]/Q1:*?"),
                ExportTable.FromCells(["A"], [["2"]], "Report[2026]/Q1:*?"),
                ExportTable.FromCells(["A"], [["3"]], new string('x', 60)),
            ],
        };

        using var workbook = await ExportWorkbookAsync(TestData.Request(document));

        Assert.Equal(3, workbook.Worksheets.Count);
        foreach (var sheet in workbook.Worksheets)
        {
            Assert.True(sheet.Name.Length <= 31, $"Sheet name too long: {sheet.Name}");
            Assert.DoesNotContain('[', sheet.Name);
            Assert.DoesNotContain('/', sheet.Name);
            Assert.DoesNotContain(':', sheet.Name);
        }

        Assert.Equal(3, workbook.Worksheets.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Strings_that_look_like_formulas_stay_text()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.InjectionTable()));
        var sheet = workbook.Worksheet("Injection");

        Assert.Equal(XLDataType.Text, sheet.Cell(2, 1).DataType);
        Assert.Equal("=1+1", sheet.Cell(2, 1).GetString());
        Assert.False(sheet.Cell(2, 1).HasFormula);
        Assert.False(sheet.Cell(3, 1).HasFormula);
    }

    [Fact]
    public async Task Formulas_are_written_only_when_explicitly_requested()
    {
        var table = ExportTable.Create(TestData.Sales(3), "Sales")
            .Column("Region", x => x.Region)
            .Column("Revenue", x => x.Revenue, format: "N2")
            .WithTotals("Total", new ExcelFormula("SUM(B2:B{lastRow})"))
            .Build();

        using var workbook = await ExportWorkbookAsync(TestData.Request(table));
        var sheet = workbook.Worksheet("Sales");
        var totalsCell = sheet.Cell(5, 2);

        Assert.True(totalsCell.HasFormula);
        Assert.Equal("SUM(B2:B4)", totalsCell.FormulaA1);
        Assert.True(sheet.Cell(5, 1).Style.Font.Bold);
    }

    [Fact]
    public async Task Text_mode_writes_everything_as_strings()
    {
        var options = new ExcelExportOptions { TypedCells = false };
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesTable(1), options));

        Assert.Equal(XLDataType.Text, workbook.Worksheet("Sales").Cell(2, 1).DataType);
    }

    [Fact]
    public async Task Explicit_column_widths_are_honoured()
    {
        var table = ExportTable.Create(TestData.Sales(1), "Widths")
            .Column("Region", x => x.Region, width: 42)
            .Build();

        using var workbook = await ExportWorkbookAsync(TestData.Request(table));
        Assert.Equal(42d, workbook.Worksheet("Widths").Column(1).Width, 1);
    }

    [Fact]
    public async Task Round_trips_unicode_and_nulls()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.UnicodeTable()));
        var sheet = workbook.Worksheet("Unicode");

        Assert.Equal("значение", sheet.Cell(2, 1).GetString());
        Assert.Equal("日本語のテキスト", sheet.Cell(2, 2).GetString());
        Assert.Equal("🚀🌍", sheet.Cell(2, 3).GetString());
        Assert.True(sheet.Cell(4, 1).IsEmpty());
    }

    [Fact]
    public async Task Sets_workbook_metadata()
    {
        var options = new ExcelExportOptions { Author = "Albin" };
        using var workbook = await ExportWorkbookAsync(TestData.Request(TestData.SalesDocument(1), options));

        Assert.Equal("Quarterly sales", workbook.Properties.Title);
        Assert.Equal("Albin", workbook.Properties.Author);
    }

    [Fact]
    public async Task A_document_with_no_tables_still_produces_a_valid_workbook()
    {
        var document = ExportDocument.Builder("Text only").Paragraph("No tables here.").Build();
        using var workbook = await ExportWorkbookAsync(TestData.Request(document));
        Assert.Single(workbook.Worksheets);
    }

    [Fact]
    public async Task An_empty_table_writes_only_the_header()
    {
        using var workbook = await ExportWorkbookAsync(TestData.Request(ExportTable.Empty(["A", "B"], "Empty")));
        var sheet = workbook.Worksheet("Empty");

        Assert.Equal("A", sheet.Cell(1, 1).GetString());
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
        Assert.False(sheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public async Task Writes_a_moderately_large_sheet()
    {
        const int rows = 20_000;
        var table = ExportTable.Create(TestData.Sales(rows), "Large")
            .Column("Id", x => x.Id)
            .Column("Region", x => x.Region)
            .Column("Revenue", x => x.Revenue)
            .WithHeaderBehaviour(freezeHeader: true, autoFilter: false)
            .Build();

        var options = new ExcelExportOptions { AutoFitColumns = false };
        using var workbook = await ExportWorkbookAsync(TestData.Request(table, options));

        Assert.Equal(rows + 1, workbook.Worksheet("Large").LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Writes_to_a_non_seekable_destination()
    {
        using var inner = new MemoryStream();
        await using (var destination = new WriteOnlyStream(inner))
        {
            await _exporter.ExportAsync(TestData.Request(TestData.SalesTable(3)), destination);
        }

        inner.Position = 0;
        using var workbook = new XLWorkbook(inner);
        Assert.Equal("Sales", workbook.Worksheets.First().Name);
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

    private async Task<XLWorkbook> ExportWorkbookAsync(ExportRequest request)
    {
        var stream = new MemoryStream();
        await _exporter.ExportAsync(request, stream);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }
}
