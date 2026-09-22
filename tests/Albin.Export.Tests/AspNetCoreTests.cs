using Albin.Export.Abstractions;
using Albin.Export.AspNetCore;
using Microsoft.AspNetCore.Mvc;

namespace Albin.Export.Tests;

public sealed class AspNetCoreTests
{
    [Theory]
    [InlineData("pdf", ExportFormat.Pdf)]
    [InlineData("PDF", ExportFormat.Pdf)]
    [InlineData(".xlsx", ExportFormat.Excel)]
    [InlineData("excel", ExportFormat.Excel)]
    [InlineData("csv", ExportFormat.Csv)]
    [InlineData("html", ExportFormat.Html)]
    [InlineData("docx", ExportFormat.Word)]
    [InlineData("json", ExportFormat.Json)]
    [InlineData("xml", ExportFormat.Xml)]
    [InlineData("md", ExportFormat.Markdown)]
    [InlineData("zip", ExportFormat.Zip)]
    [InlineData("png", ExportFormat.Png)]
    [InlineData("svg", ExportFormat.Svg)]
    [InlineData("echarts", ExportFormat.ECharts)]
    public void TryParse_accepts_names_and_extensions(string value, ExportFormat expected)
    {
        Assert.True(ExportFormats.TryParse(value, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("exe")]
    [InlineData("javascript")]
    public void TryParse_rejects_unknown_values(string? value)
        => Assert.False(ExportFormats.TryParse(value, out _));

    [Fact]
    public void Parse_rejects_unregistered_formats()
    {
        var service = new ExportService([new Albin.Export.Csv.CsvExporter()]);
        Assert.Throws<ExportFormatNotSupportedException>(() => service.Parse("pdf"));
        Assert.Equal(ExportFormat.Csv, service.Parse("csv"));
    }

    [Fact]
    public void ToFileResult_carries_stream_type_and_download_name()
    {
        using var stream = new MemoryStream("hello"u8.ToArray());
        using var result = new ExportResult("sales.csv", ExportContentTypes.Csv, stream, ExportFormat.Csv);

        var file = result.ToFileResult();

        Assert.Same(result.Stream, file.FileStream);
        Assert.Equal(ExportContentTypes.Csv, file.ContentType);
        Assert.Equal("sales.csv", file.FileDownloadName);
        Assert.IsType<FileStreamResult>(file);
    }
}
