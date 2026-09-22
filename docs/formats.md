# Export formats

Every exporter implements `IExporter` and is reached through `IExportService`.

## PDF (`Albin.Export.Pdf`)

PDFsharp + MigraDoc. Headings, paragraphs, paginated tables with repeating headers, images, charts, page headers/footers with "Page X of Y", margins, fonts, orientation, page size. Written to a stream; no temp files.

Fonts come from `SystemFontResolver`, which indexes Windows/Linux/macOS font directories. Slim Linux images need a font package (`fonts-dejavu-core`).

```csharp
await export.ExportAsync(document, ExportFormat.Pdf, new PdfExportOptions
{
    FileName = "sales",
    Landscape = true,
    PageSize = PdfPageSize.A4,
    Author = "Contoso",
});
```

## Excel (`Albin.Export.Excel`)

ClosedXML `.xlsx`. One worksheet per table, typed cells, header styling, freeze panes, auto-filter, column sizing, number formats, totals, optional `ExcelFormula` values. Tables longer than Excel's row limit continue on extra sheets.

ClosedXML builds the workbook in memory. For hundreds of thousands of rows prefer CSV, or turn `AutoFitColumns` off.

```csharp
.WithTotals("Total", new ExcelFormula("SUM(B2:B{lastRow})"))
```

`{lastRow}` is replaced with the last data row number.

## CSV (`Albin.Export.Csv`)

CsvHelper. RFC 4180 quoting, configurable delimiter/culture/new line, UTF-8 with BOM by default (Excel-friendly). Formula-injection payloads (`=`, `+`, `-`, `@`, ...) are escaped by default. Rows stream to the destination.

## HTML (`Albin.Export.Html`)

Standalone, responsive, print-friendly document. Every data value is HTML-encoded. Charts default to inline SVG. Set `HtmlChartMode.EChartsClient` to emit a container for the browser. Use `Fragment = true` to embed in a layout.

Razor templates live in `Albin.Export.AspNetCore` (`AddRazorRendering` / `AddRazorHtml`) so the core HTML package does not depend on MVC.

## Word (`Albin.Export.Word`)

Open XML SDK `.docx`. Headings with outline levels, paragraphs, tables with repeating header rows, images, charts, page headers and footers with PAGE/NUMPAGES fields.

## JSON (`Albin.Export.Json`)

`System.Text.Json`, UTF-8. Serializes `ExportRequest.Data` (the original DTOs) unless `SerializeDocument` is set. Configurable `JsonSerializerOptions`. Default encoder is HTML-safe.

## XML (`Albin.Export.Xml`)

Built-in `XmlWriter` only — the exporter never parses caller markup. Element names are passed through `XmlConvert.EncodeLocalName`. Optional `XmlSerializer` mode for DTO-shaped output (`UseXmlSerializer = true`). No DTDs, no resolvers.

## Markdown (`Albin.Export.Markdown`)

Hand-written GitHub-flavoured headings, paragraphs and pipe tables. No Markdown library. Pipes, backslashes and line breaks in cells are escaped.

## ZIP (`Albin.Export.Zip`)

`System.IO.Compression`. Bundle several formats:

```csharp
var zip = await export.ExportBundleAsync(
    request,
    [ExportFormat.Pdf, ExportFormat.Excel, ExportFormat.Csv]);
```

Entry names cannot traverse (`../`, absolute paths, drive letters, backslashes). Duplicates get a numeric suffix so one entry cannot overwrite another.

## Charts (`Albin.Export.Chart`)

| Format | Output |
| --- | --- |
| `ExportFormat.ECharts` | Apache ECharts `option` JSON for `echarts.init(el).setOption(option)` in the browser |
| `ExportFormat.Svg` | Server-side SVG, no scripts |
| `ExportFormat.Png` | Server-side raster, no Skia/System.Drawing/browser |

Feed them a `ChartSpec` (bar, line, area, pie). Do not add Playwright/Chromium just to get a PNG.
