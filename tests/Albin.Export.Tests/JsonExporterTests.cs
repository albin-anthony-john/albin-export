using System.Text.Json;
using Albin.Export.Abstractions;
using Albin.Export.Documents;
using Albin.Export.Json;

namespace Albin.Export.Tests;

public sealed class JsonExporterTests
{
    private readonly JsonExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Json, _exporter.Format);
        Assert.Equal(".json", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Json, _exporter.ContentType);
    }

    [Fact]
    public async Task Serializes_the_original_dtos_with_camel_case_names()
    {
        var request = ExportRequest.FromItems(TestData.Sales(2));
        var json = await TestData.RenderTextAsync(_exporter, request);

        using var document = JsonDocument.Parse(json);
        var first = document.RootElement[0];

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal(1, first.GetProperty("id").GetInt32());
        Assert.Equal("North", first.GetProperty("region").GetString());
        Assert.False(first.GetProperty("active").GetBoolean());
        Assert.True(document.RootElement[1].GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Keeps_nested_structure_of_the_payload()
    {
        var payload = new { Total = 3, Items = TestData.Sales(3), Nested = new { Flag = true } };
        var json = await TestData.RenderTextAsync(_exporter, ExportRequest.FromObject(payload));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(3, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.True(document.RootElement.GetProperty("nested").GetProperty("flag").GetBoolean());
    }

    [Fact]
    public async Task Projects_the_document_when_no_payload_is_available()
    {
        var request = new ExportRequest(ExportDocument.FromTable(TestData.SalesTable(2)));
        var json = await TestData.RenderTextAsync(_exporter, request);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal("North", document.RootElement[0].GetProperty("Region").GetString());
    }

    [Fact]
    public async Task Projects_multiple_tables_into_a_named_map()
    {
        var document = new ExportDocument
        {
            Title = "Two tables",
            Sections = [TestData.SalesTable(1), ExportTable.FromCells(["X"], [["y"]], "Second")],
        };

        var json = await TestData.RenderTextAsync(_exporter, new ExportRequest(document));

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("Two tables", parsed.RootElement.GetProperty("title").GetString());
        var tables = parsed.RootElement.GetProperty("tables");
        Assert.True(tables.TryGetProperty("Sales", out _));
        Assert.True(tables.TryGetProperty("Second", out _));
    }

    [Fact]
    public async Task SerializeDocument_forces_the_tabular_projection()
    {
        var request = ExportRequest.FromItems(
            TestData.Sales(1),
            new JsonExportOptions { SerializeDocument = true });

        var json = await TestData.RenderTextAsync(_exporter, request);
        Assert.Contains("\"Region\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Escapes_html_sensitive_characters_by_default()
    {
        var request = ExportRequest.FromItems(new[] { new { Html = "<script>alert('x')</script>" } });
        var json = await TestData.RenderTextAsync(_exporter, request);

        Assert.DoesNotContain("<script>", json, StringComparison.Ordinal);
        Assert.Contains("\\u003C", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serializer_options_are_configurable()
    {
        var options = new JsonExportOptions
        {
            SerializerOptions = new JsonSerializerOptions { WriteIndented = false },
        };

        var json = await TestData.RenderTextAsync(_exporter, ExportRequest.FromItems(TestData.Sales(1), options));

        Assert.DoesNotContain('\n', json);
        Assert.Contains("\"Region\":\"North\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_no_byte_order_mark()
    {
        var bytes = await TestData.RenderAsync(_exporter, ExportRequest.FromItems(TestData.Sales(1)));
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public async Task Round_trips_unicode_and_nulls()
    {
        var items = new[]
        {
            new { Name = "日本語 🚀", Value = (int?)null },
            new { Name = "Ключ", Value = (int?)7 },
        };

        var json = await TestData.RenderTextAsync(_exporter, ExportRequest.FromItems(items));

        using var document = JsonDocument.Parse(json);
        Assert.Equal("日本語 🚀", document.RootElement[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement[0].GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Empty_dataset_becomes_an_empty_array()
    {
        var json = await TestData.RenderTextAsync(_exporter, ExportRequest.FromItems(Array.Empty<SalesRecord>()));
        Assert.Equal("[]", json.Trim());
    }

    [Fact]
    public async Task Serializes_asynchronous_sequences()
    {
        var table = ExportTable.FromItems(TestData.SalesAsync(3), "Async");
        var request = new ExportRequest(ExportDocument.FromTable(table));

        var json = await TestData.RenderTextAsync(_exporter, request);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(3, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestData.RenderAsync(_exporter, ExportRequest.FromItems(TestData.Sales(500)), cts.Token));
    }

    [Fact]
    public async Task Rejects_null_arguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _exporter.ExportAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(ExportRequest.FromItems(TestData.Sales(1)), null!));
    }
}
