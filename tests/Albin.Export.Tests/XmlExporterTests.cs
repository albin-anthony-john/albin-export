using System.Xml;
using System.Xml.Linq;
using Albin.Export.Abstractions;
using Albin.Export.Documents;
using Albin.Export.Xml;

namespace Albin.Export.Tests;

public sealed class XmlExporterTests
{
    private readonly XmlExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Xml, _exporter.Format);
        Assert.Equal(".xml", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Xml, _exporter.ContentType);
    }

    [Fact]
    public async Task Writes_one_element_per_row_and_column()
    {
        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(3)));
        var document = XDocument.Parse(xml);

        Assert.Equal("export", document.Root!.Name.LocalName);
        var table = document.Root.Element("Sales")!;
        Assert.Equal(3, table.Elements("row").Count());
        Assert.Equal("North", table.Elements("row").First().Element("Region")!.Value);
    }

    [Fact]
    public async Task Element_names_are_configurable()
    {
        var options = new XmlExportOptions { RootElementName = "report", RowElementName = "record" };
        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(1), options));
        var document = XDocument.Parse(xml);

        Assert.Equal("report", document.Root!.Name.LocalName);
        Assert.Single(document.Root.Element("Sales")!.Elements("record"));
    }

    [Fact]
    public async Task Column_headers_that_are_invalid_xml_names_are_encoded_not_injected()
    {
        var table = ExportTable.FromCells(
            ["with space", "<injected>", "1starts-with-digit", "ok"],
            [["a", "b", "c", "d"]],
            "Weird headers");

        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(table));
        var document = XDocument.Parse(xml);
        var row = document.Descendants("row").Single();

        Assert.Equal(4, row.Elements().Count());
        Assert.DoesNotContain("<injected>", xml, StringComparison.Ordinal);
        Assert.Equal("ok", row.Elements().Last().Name.LocalName);
    }

    [Fact]
    public async Task Escapes_markup_in_values()
    {
        var table = ExportTable.FromCells(
            ["Payload"],
            [["</row><injected>evil</injected>"], ["a & b < c"]]);

        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(table));
        var document = XDocument.Parse(xml);

        Assert.Equal(2, document.Descendants("row").Count());
        Assert.Equal("</row><injected>evil</injected>", document.Descendants("Payload").First().Value);
        Assert.Contains("&amp;", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Produces_no_doctype_or_external_references()
    {
        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(2)));

        Assert.DoesNotContain("<!DOCTYPE", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!ENTITY", xml, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_declaration_can_be_omitted()
    {
        var xml = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(TestData.SalesTable(1), new XmlExportOptions { OmitXmlDeclaration = true }));

        Assert.StartsWith("<export", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task XmlSerializer_mode_mirrors_the_dto_shape()
    {
        var payload = new PurchaseOrder
        {
            Number = "PO-1",
            Lines = [new OrderLine { Sku = "A", Quantity = 2 }, new OrderLine { Sku = "B", Quantity = 5 }],
        };

        var request = ExportRequest.FromObject(payload, new XmlExportOptions { UseXmlSerializer = true });
        var xml = await TestData.RenderTextAsync(_exporter, request);
        var document = XDocument.Parse(xml);

        Assert.Equal(nameof(PurchaseOrder), document.Root!.Name.LocalName);
        Assert.Equal("PO-1", document.Root.Element("Number")!.Value);
        Assert.Equal(2, document.Root.Element("Lines")!.Elements("OrderLine").Count());
    }

    [Fact]
    public async Task Round_trips_unicode()
    {
        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.UnicodeTable()));
        var document = XDocument.Parse(xml);

        Assert.Contains("日本語のテキスト", document.ToString(), StringComparison.Ordinal);
        Assert.Contains("🚀🌍", document.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_dataset_produces_an_empty_table_element()
    {
        var xml = await TestData.RenderTextAsync(
            _exporter,
            TestData.Request(ExportTable.Empty(["A"], "Empty")));

        var document = XDocument.Parse(xml);
        Assert.Empty(document.Descendants("row"));
    }

    [Fact]
    public async Task Output_is_parseable_with_a_hardened_reader()
    {
        var xml = await TestData.RenderTextAsync(_exporter, TestData.Request(TestData.SalesTable(2)));

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 1024,
        };

        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var rows = 0;
        while (reader.Read())
        {
            if (reader is { NodeType: XmlNodeType.Element, Name: "row" })
            {
                rows++;
            }
        }

        Assert.Equal(2, rows);
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(_exporter, TestData.Request(TestData.SalesTable(500)), cts.Token));
    }

    public sealed class PurchaseOrder
    {
        public string Number { get; set; } = string.Empty;

        public List<OrderLine> Lines { get; set; } = [];
    }

    public sealed class OrderLine
    {
        public string Sku { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
