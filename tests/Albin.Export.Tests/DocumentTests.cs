using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void FromItems_honours_column_attributes_and_ignores()
    {
        var items = new[]
        {
            new AttributedDto { Id = 7, Name = "alpha", Total = 12.5m, Secret = "nope" },
        };

        var table = ExportTable.FromItems(items, "Attributed");

        Assert.Equal(["Identifier", "Name", "Amount"], table.Columns.Select(c => c.Header));
        Assert.Equal("N2", table.Columns[2].Format);
        Assert.Equal(ExportAlignment.Right, table.Columns[2].Alignment);

        var row = table.Rows.ReadAsync().ToBlockingEnumerable().Single();
        Assert.Equal(7, row[0]);
        Assert.Equal("alpha", row[1]);
        Assert.Equal(12.5m, row[2]);
        Assert.Equal(3, row.FieldCount);
    }

    [Fact]
    public void Builder_assembles_sections_in_order()
    {
        var document = ExportDocument.Builder("Title")
            .Subtitle("Sub")
            .Footer("Foot")
            .Heading("H")
            .Paragraph("P")
            .Table(TestData.SalesTable(1))
            .Chart(TestData.RevenueChart())
            .PageBreak()
            .Build();

        Assert.Equal("Title", document.Title);
        Assert.Equal("Sub", document.Subtitle);
        Assert.Equal("Foot", document.Footer);
        Assert.Equal(5, document.Sections.Count);
        Assert.Single(document.Tables());
    }

    [Fact]
    public void Request_resolves_a_safe_file_name()
    {
        var request = ExportRequest.FromItems(
            TestData.Sales(1),
            new ExportOptions { FileName = "../../evil" });

        Assert.Equal("evil", request.ResolveFileNameStem());
    }

    [Fact]
    public async Task Async_row_source_is_honoured()
    {
        var table = ExportTable
            .Create(TestData.SalesAsync(4), "Async")
            .Column("Id", x => x.Id)
            .Build();

        var ids = new List<object?>();
        await foreach (var row in table.Rows.ReadAsync())
        {
            ids.Add(row[0]);
        }

        Assert.Equal([1, 2, 3, 4], ids);
    }
}
