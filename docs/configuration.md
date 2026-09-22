# Configuration and options

All format option types derive from `ExportOptions` and can be passed as the `options` argument of `ExportAsync`.

## Shared (`ExportOptions`)

| Property | Default | Purpose |
| --- | --- | --- |
| `FileName` | derived from title | Stem only. Sanitized; path segments are dropped. |
| `IncludeHeaders` | `true` | Write table header rows. |
| `Culture` | invariant | Number/date formatting. |
| `DateTimeFormat` | `yyyy-MM-dd HH:mm:ss` | Fallback when a column has no format. |
| `NullText` | `""` | Replacement for null cells. |
| `Author` | `null` | Document metadata where supported. |

## PDF (`PdfExportOptions`)

`FontFamily`, `FontSize`, `PageSize` (A4/Letter/A3/A5), `Landscape`, `MarginTop/Bottom/Left/Right` (points), `HeaderText`, `FooterText`, `IncludePageNumbers`, `RepeatTableHeaders`, `StripedTables`.

## Excel (`ExcelExportOptions`)

`StyleHeaderRow`, `AutoFitColumns`, `MaxColumnWidth`, `DefaultNumberFormat`, `DefaultDateFormat`, `TypedCells`, `SplitLargeTables`.

Turn `AutoFitColumns` off for large sheets. Set `ExportColumn.Width` when you still want a sensible width.

## CSV (`CsvExportOptions`)

`Delimiter`, `Quote`, `NewLine`, `WriteByteOrderMark` (true), `EscapeFormulas` (true), `IncludeAllTables` (false — CSV has no sheets).

## HTML (`HtmlExportOptions`)

`Fragment`, `IncludeDefaultStyles`, `AdditionalCss` (must be application-controlled, never user input), `Language`, `ChartMode`, `EChartsScriptUrl`, `StripedTables`.

## Word (`WordExportOptions`)

`FontFamily`, `FontSize`, `PageSize`, `Landscape`, margins, header/footer text, page numbers. See the type for the full list.

## JSON (`JsonExportOptions`)

`SerializerOptions` (defaults to web defaults, indented, HTML-safe encoder), `SerializeDocument`.

## XML (`XmlExportOptions`)

`RootElementName`, `RowElementName`, `Indent`, `OmitXmlDeclaration`, `UseXmlSerializer`.

## Markdown (`MarkdownExportOptions`)

`IncludeTitle`, `IncludeAlignmentMarkers`, `ChartsAsTables`, `EmbedImages`.

## ZIP (`ZipExportOptions`)

`Entries` (`IReadOnlyList<ZipEntrySource>`), `CompressionLevel`. Prefer `ExportBundleAsync` unless you need a custom layout.

## Charts (`ChartExportOptions`)

`Chart` (explicit spec; otherwise the first document chart, else derived from the first table), `IndentJson`, `MaxDerivedCategories`. Pixel size comes from `ChartSpec.Width` / `ChartSpec.Height`.

## Column attributes

When using `ExportTable.FromItems<T>`:

```csharp
public sealed class InvoiceDto
{
    [ExportColumn(Header = "Number", Order = -1)]
    public string Number { get; set; } = "";

    [ExportColumn(Header = "Total", Format = "N2", Alignment = ExportAlignment.Right)]
    public decimal Total { get; set; }

    [ExportIgnore]
    public string InternalId { get; set; } = "";
}
```
