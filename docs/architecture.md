# Architecture

Albin.Export is a presentation layer. It does not know about your database, repositories, or domain.

```
Controller
    → Application / report service   (authorization, queries, DTO mapping)
        → Albin.Export               (layout and file bytes)
            → format-specific IExporter
```

## Public surface

Keep this small and stable:

| Type | Role |
| --- | --- |
| `IExportService` | Facade used by applications. |
| `IExporter` | One implementation per format. |
| `ExportRequest` | Document + optional raw payload + options. |
| `ExportResult` | `FileName`, `ContentType`, `Stream`. Dispose it (or let ASP.NET Core do it). |
| `ExportFormat` | Enum of supported formats. |
| `ExportOptions` | Shared options; format packages subclass it. |
| `ExportDocument` / `ExportTable` | Format-independent layout. |

## Two views of the same data

`ExportRequest` carries:

- **Document** — tables, headings, paragraphs, charts. Used by PDF, Excel, CSV, HTML, Word, Markdown, PNG, SVG, ECharts.
- **Data** — the original object graph. Used by JSON and XML so nesting survives.

`ExportRequest.FromItems(rows)` fills both: JSON serializes `rows` directly; Excel renders the derived table.

## Streaming model

`IExporter.ExportAsync(request, destination, cancellationToken)` writes into a caller-owned stream and **must not dispose it**.

- CSV, JSON, HTML, XML, Markdown, SVG write through the destination (including an HTTP response body).
- PDF, Excel, Word, ZIP, PNG need a seekable stream internally. `ExportStreams.WriteSeekableAsync` writes in place when the destination is already seekable, otherwise it uses one intermediate `MemoryStream`.

`IExportService.ExportAsync` always buffers into a `MemoryStream` so the result can be rewound and returned from a controller. `ExportToAsync` / `ExportToResponseAsync` skip that buffer.

## Dependency injection

```csharp
services.AddAlbinExport(export => export.AddPdf().AddExcel());
```

Each format package adds an extension on `ExportServiceBuilder`. Registering the same exporter type twice is a no-op. Registering a second exporter for the same `ExportFormat` replaces the first (last one wins), which is how Razor HTML replaces the built-in HTML writer.

## What the library will not do

- Open a connection, run SQL, or accept an `IQueryable`.
- Read files from arbitrary paths supplied in an export request.
- Execute HTML, JavaScript, or Excel formulas from untrusted cell values.
- Spin up a browser to screenshot ECharts.

Charts are a `ChartSpec`. Server-side SVG/PNG are generated in managed code. The ECharts exporter emits an option JSON for the **client** to render.
