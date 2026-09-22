# Albin.Export

Production-ready, reusable export framework for ASP.NET Core and .NET 10.

Albin.Export turns **already-prepared DTOs and report models** into PDF, Excel, CSV, HTML, Word, JSON, XML, Markdown, ZIP, PNG, SVG, and Apache ECharts option files. It never queries a database, never touches a repository, and never embeds business logic.

```csharp
var result = await exportService.ExportAsync(reportData, ExportFormat.Pdf, options, cancellationToken);
return File(result.Stream, result.ContentType, result.FileName);
```

## Why this library

- **One API** for every format: `IExportService.ExportAsync`.
- **Modular NuGet packages** so applications only take the dependencies they use.
- **Stream-first**: exporters write to a `Stream`, with `CancellationToken` support.
- **Cross-platform**: Windows, Linux, and Docker. No Office, no browser automation.
- **Safe defaults**: file-name sanitization, ZIP path-traversal protection, HTML/XML encoding, CSV formula escaping, secure XML writing.
- **Permissive licenses only** (MIT / Apache-2.0 / MS-PL).

## Packages

| Package | What it adds | Extra dependency |
| --- | --- | --- |
| `Albin.Export` | Core abstractions, `IExportService`, document/table model | Microsoft.Extensions.DI.Abstractions |
| `Albin.Export.Pdf` | PDF via PDFsharp/MigraDoc | PDFsharp-MigraDoc |
| `Albin.Export.Excel` | `.xlsx` via ClosedXML | ClosedXML |
| `Albin.Export.Csv` | CSV via CsvHelper | CsvHelper |
| `Albin.Export.Html` | Print-friendly HTML (encoded) | — |
| `Albin.Export.Word` | `.docx` via Open XML SDK | DocumentFormat.OpenXml |
| `Albin.Export.Json` | UTF-8 JSON via System.Text.Json | — |
| `Albin.Export.Xml` | UTF-8 XML via built-in writers | — |
| `Albin.Export.Markdown` | GitHub-flavoured Markdown | — |
| `Albin.Export.Zip` | ZIP bundling | — |
| `Albin.Export.Chart` | ECharts option JSON, server-side SVG and PNG | — |
| `Albin.Export.AspNetCore` | `File(...)` helpers, streaming responses, Razor HTML | ASP.NET Core shared framework |

Install only what you need:

```bash
dotnet add package Albin.Export
dotnet add package Albin.Export.Pdf
dotnet add package Albin.Export.Excel
dotnet add package Albin.Export.Csv
dotnet add package Albin.Export.AspNetCore
```

Target framework: **.NET 10** (current LTS). The public API is kept small so multi-targeting later is straightforward.

## Quick start

```csharp
builder.Services.AddAlbinExport(export => export
    .AddPdf()
    .AddExcel()
    .AddCsv()
    .AddJson());
```

```csharp
public sealed class ReportsController(IExportService export) : ControllerBase
{
    [HttpGet("sales.pdf")]
    public async Task<IActionResult> SalesPdf(CancellationToken cancellationToken)
    {
        var rows = await reports.GetSalesAsync(cancellationToken); // your app, not this library

        var result = await export.ExportAsync(
            rows,
            ExportFormat.Pdf,
            new PdfExportOptions { FileName = "sales", Landscape = true },
            cancellationToken);

        return File(result.Stream, result.ContentType, result.FileName);
    }
}
```

The same call works for every format:

```csharp
await export.ExportAsync(data, ExportFormat.Excel);
await export.ExportAsync(data, ExportFormat.Csv);
await export.ExportAsync(data, ExportFormat.Json);
```

## Architecture

```
Controller
    ↓
Application / report service     ← queries data, builds DTOs
    ↓
Albin.Export                     ← presentation only
    ↓
PDF / Excel / CSV / HTML / DOCX / JSON / XML / Markdown / ZIP / charts
```

The library receives an `ExportRequest` (a document, optional raw payload, and options) and writes bytes. It has no EF Core, Dapper, SQL, or repository types.

Use `ExportTable.Create(...)` when you want an explicit, reflection-free column list. Use `ExportAsync<T>(IEnumerable<T>)` when a DTO's public properties are already the report shape.

```csharp
var document = ExportDocument.Builder("Quarterly sales")
    .Subtitle("Q1 2026")
    .Heading("Summary")
    .Paragraph("Revenue grew across every region.")
    .Table(ExportTable.Create(rows, "Sales")
        .Column("Region", x => x.Region)
        .Column("Revenue", x => x.Revenue, format: "N2")
        .Build())
    .Chart(new ChartSpec(ChartType.Bar, categories, series) { Title = "Revenue" })
    .Build();

var result = await export.ExportAsync(document, ExportFormat.Pdf);
```

## Documentation

- [Getting started](docs/getting-started.md)
- [Installation](docs/installation.md)
- [Architecture](docs/architecture.md)
- [Export formats](docs/formats.md)
- [ASP.NET Core integration](docs/aspnetcore.md)
- [Configuration / options](docs/configuration.md)
- [Performance](docs/performance.md)
- [Security](docs/security.md)
- [Custom exporters](docs/extending.md)
- [NuGet packaging](docs/nuget.md)

## Demo and tests

```bash
dotnet test
dotnet run --project samples/Albin.Export.Demo
dotnet run --project benchmarks/Albin.Export.Benchmarks -c Release
dotnet pack -c Release
```

Tests use xUnit v3 on Microsoft.Testing.Platform (the `test.runner` entry in `global.json`).

The demo is a Web API on `http://localhost:5088`. `GET /api` returns a JSON catalog. `GET /api/files/{format}` returns a complete file; `GET /api/stream/{format}` writes the export straight to the response body. Linux/Docker PDF output needs at least one font package (the sample Dockerfile installs `fonts-dejavu-core`).

## License

MIT. See [LICENSE](LICENSE).
