# ASP.NET Core integration

Package: `Albin.Export.AspNetCore`.

## Registration

```csharp
builder.Services.AddControllersWithViews(); // only required for Razor export
builder.Services.AddAlbinExport(export => export
    .AddPdf()
    .AddExcel()
    .AddCsv()
    .AddHtml()
    .AddRazorRendering());
```

## Controller

```csharp
[HttpGet("reports/sales")]
public async Task<IActionResult> Sales(string format, CancellationToken cancellationToken)
{
    var parsed = export.Parse(format); // "pdf", "xlsx", "csv", ...
    var rows = await reports.GetSalesAsync(cancellationToken);

    var result = await export.ExportAsync(rows, parsed, cancellationToken: cancellationToken);
    return File(result.Stream, result.ContentType, result.FileName);
}
```

Equivalent helpers:

```csharp
return result.ToFileResult();
```

## Minimal APIs

```csharp
app.MapGet("/reports/sales.{format}", async (
    string format,
    IExportService export,
    ReportService reports,
    CancellationToken cancellationToken) =>
{
    if (!ExportFormats.TryParse(format, out var parsed) || !export.Supports(parsed))
        return Results.BadRequest();

    var result = await export.ExportAsync(await reports.GetSalesAsync(cancellationToken), parsed, cancellationToken: cancellationToken);
    return result.ToHttpResult();
});
```

`ToHttpResult(inline: true)` is useful for PDF/HTML previews.

## Streaming (no extra memory buffer)

CSV, JSON, HTML, XML, Markdown and SVG can write straight to `HttpResponse.Body`:

```csharp
[HttpGet("reports/sales.csv")]
public Task Csv(CancellationToken cancellationToken)
    => export.ExportToResponseAsync(Response, rows, ExportFormat.Csv, cancellationToken: cancellationToken);
```

Headers are sent before rendering finishes, so a mid-export failure cannot become a 500 body. Use the buffered `ExportAsync` when you need to fail cleanly.

## Razor templates

Keep HTML generation in the ASP.NET Core host when you already have views:

```csharp
builder.Services.AddAlbinExport(export => export.AddRazorRendering());

var html = await razor.RenderToResultAsync("Reports/Invoice", invoice, "invoice", cancellationToken);
return html.ToFileResult();
```

`AddRazorHtml()` registers `RazorHtmlExporter` as `ExportFormat.Html`. Call it **after** `AddHtml()` if you want Razor to replace the built-in writer. The request must carry `RazorExportOptions("Reports/Invoice")`.

Razor encodes by default (`@Model.Title`). Do not emit untrusted values with `Html.Raw`.

## Sample

`samples/Albin.Export.Demo` is a Web API (`dotnet run --project samples/Albin.Export.Demo`):

- `GET /api` — JSON catalog of every endpoint
- `GET /api/files/{format}` — complete file (`File(stream, contentType, fileName)`)
- `GET /api/stream/{format}` — bytes written straight to the response body
- `GET /api/rows/{format}` and `GET /api/rows/{format}/stream` — DTO rows only
- `GET /api/bundle` — ZIP of PDF, Excel, CSV and JSON

Formats: `pdf`, `xlsx`, `csv`, `html`, `docx`, `json`, `xml`, `md`, `svg`, `png`, `echarts`. Optional `?count=` controls row count.
