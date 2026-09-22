using Albin.Export.Abstractions;
using Albin.Export.AspNetCore;
using Albin.Export.Chart;
using Albin.Export.Csv;
using Albin.Export.Excel;
using Albin.Export.Html;
using Albin.Export.Json;
using Albin.Export.Markdown;
using Albin.Export.Pdf;
using Albin.Export.Word;
using Albin.Export.Xml;
using Albin.Export.Zip;
using Microsoft.AspNetCore.Mvc;

namespace Albin.Export.Demo;

[ApiController]
[Route("api")]
public sealed class ExportsController : ControllerBase
{
    private static readonly string[] FileFormats =
    [
        "pdf", "xlsx", "csv", "html", "docx", "json", "xml", "md", "svg", "png", "echarts",
    ];

    private readonly IExportService _export;
    private readonly SalesReportService _reports;

    public ExportsController(IExportService export, SalesReportService reports)
    {
        _export = export;
        _reports = reports;
    }

    /// <summary>JSON catalog of every export endpoint.</summary>
    [HttpGet]
    [HttpGet("/")]
    public IActionResult Catalog() => Ok(new
    {
        title = "Albin.Export API demo",
        note = "Buffered endpoints buffer the whole file then return File(stream, contentType, fileName). Streamed endpoints write bytes straight to the response body.",
        bufferedFiles = FileFormats.ToDictionary(f => f, f => $"/api/files/{f}"),
        streamedFiles = FileFormats.ToDictionary(f => f, f => $"/api/stream/{f}"),
        dtoRowsBuffered = FileFormats.ToDictionary(f => f, f => $"/api/rows/{f}"),
        dtoRowsStreamed = FileFormats.ToDictionary(f => f, f => $"/api/rows/{f}/stream"),
        zipBundle = "/api/bundle",
        query = new { count = "optional row count, 1-10000, default 24" },
    });

    /// <summary>Complete file: export is fully generated, then returned as a downloadable File result.</summary>
    [HttpGet("files/{format}")]
    public async Task<IActionResult> FileDownload(string format, int count = 24, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(format, out var parsed, out var error))
        {
            return error;
        }

        var result = await _export.ExportAsync(
            _reports.CreateDocument(Clamp(count)),
            parsed,
            OptionsFor(parsed),
            cancellationToken);

        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>Streamed file: bytes are written directly to the HTTP response body.</summary>
    [HttpGet("stream/{format}")]
    public async Task<IActionResult> StreamDownload(string format, int count = 24, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(format, out var parsed, out var error))
        {
            return error;
        }

        await _export.ExportToResponseAsync(
            Response,
            new ExportRequest(_reports.CreateDocument(Clamp(count)), OptionsFor(parsed)),
            parsed,
            cancellationToken: cancellationToken);

        return new EmptyResult();
    }

    /// <summary>Complete file from DTO rows only (no document headings/charts).</summary>
    [HttpGet("rows/{format}")]
    public async Task<IActionResult> RowsFile(string format, int count = 24, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(format, out var parsed, out var error))
        {
            return error;
        }

        var result = await _export.ExportAsync(
            _reports.Rows(Clamp(count)),
            parsed,
            OptionsFor(parsed, fileName: "sales-rows"),
            cancellationToken);

        return result.ToFileResult();
    }

    /// <summary>Streamed DTO rows: the exporter writes straight to the response body.</summary>
    [HttpGet("rows/{format}/stream", Order = 0)]
    public async Task<IActionResult> RowsStream(string format, int count = 24, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(format, out var parsed, out var error))
        {
            return error;
        }

        await _export.ExportToResponseAsync(
            Response,
            _reports.Rows(Clamp(count)),
            parsed,
            OptionsFor(parsed, fileName: "sales-rows-stream"),
            cancellationToken);

        return new EmptyResult();
    }

    /// <summary>ZIP containing PDF, Excel, CSV and JSON of the same report (buffered archive).</summary>
    [HttpGet("bundle")]
    public async Task<IActionResult> Bundle(int count = 24, CancellationToken cancellationToken = default)
    {
        var zip = await _export.ExportBundleAsync(
            _reports.CreateDocument(Clamp(count)),
            [ExportFormat.Pdf, ExportFormat.Excel, ExportFormat.Csv, ExportFormat.Json],
            new ExportOptions { FileName = "sales" },
            cancellationToken);

        return File(zip.Stream, zip.ContentType, zip.FileName);
    }

    private bool TryResolve(string format, out ExportFormat parsed, out IActionResult error)
    {
        parsed = default;
        error = null!;

        if (!ExportFormats.TryParse(format, out parsed) || !_export.Supports(parsed) || parsed == ExportFormat.Zip)
        {
            error = BadRequest(new
            {
                error = $"Unknown or unsupported format '{format}'.",
                allowed = FileFormats,
            });
            return false;
        }

        return true;
    }

    private static int Clamp(int count) => Math.Clamp(count, 1, 10_000);

    private static ExportOptions OptionsFor(ExportFormat format, string fileName = "sales") => format switch
    {
        ExportFormat.Pdf => new PdfExportOptions { FileName = fileName, Landscape = true, Author = "Albin.Export Demo" },
        ExportFormat.Excel => new ExcelExportOptions { FileName = fileName, Author = "Albin.Export Demo" },
        ExportFormat.Csv => new CsvExportOptions { FileName = fileName },
        ExportFormat.Html => new HtmlExportOptions { FileName = fileName },
        ExportFormat.Word => new WordExportOptions { FileName = fileName, Author = "Albin.Export Demo" },
        ExportFormat.Json => new JsonExportOptions { FileName = fileName },
        ExportFormat.Xml => new XmlExportOptions { FileName = fileName },
        ExportFormat.Markdown => new MarkdownExportOptions { FileName = fileName },
        ExportFormat.Png or ExportFormat.Svg or ExportFormat.ECharts => new ChartExportOptions { FileName = fileName },
        _ => new ExportOptions { FileName = fileName },
    };
}
