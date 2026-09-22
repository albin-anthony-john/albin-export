# Security

Albin.Export is a file generator. Treat every cell value as untrusted unless your application already sanitized it.

## File names

`ExportOptions.FileName` and `ExportResult.FileName` pass through `ExportFileName.Sanitize`:

- Only the last path segment is kept (`../../etc/passwd` → `passwd`).
- Drive letters, `:` , `/` , `\` , control characters and Windows reserved device names are removed.
- Names are capped in length.

Export requests never accept filesystem paths to write to. The library writes to the stream you pass it.

## ZIP

`ZipEntrySource` runs names through `SanitizeEntryName`. `..`, absolute paths, drive qualifiers and backslashes cannot appear in the archive. Duplicate names are renamed rather than overwritten.

## HTML

The built-in HTML exporter encodes every data-derived string with `HtmlEncoder.Default`. It does not emit inline event handlers. `AdditionalCss` is copied verbatim — supply it from your application, never from a user.

Razor views encode by default. Do not use `Html.Raw` with export data.

## CSV / Excel

`CsvExportOptions.EscapeFormulas` defaults to true, prefixing values that a spreadsheet would execute (`=`, `+`, `-`, `@`, tab, CR). Excel cells are written as typed values or text, not as evaluated formulas, unless you opt in with `ExcelFormula`.

## XML

The exporter only writes XML. It never loads user XML, never enables DTDs, and never sets an `XmlResolver`. Column headers become names via `XmlConvert.EncodeLocalName`.

## JSON

Default `JavaScriptEncoder` escapes `<`, `>` and `&`. There is no binary/polymorphic deserializer in this library.

## Charts / SVG / PNG

Labels are XML-escaped in SVG. The ECharts JSON is data, not executable script. HTML's client-side loader uses `JSON.parse` on a data attribute, never `eval`.

## PNG generation

PNG is produced by a small managed encoder. There is no native image codec and no process spawn.

## What applications must still do

- Authorize who can export which report.
- Limit row counts so an export cannot exhaust memory.
- Do not pass user-supplied CSS, Razor view names, or `ExcelFormula` strings from untrusted input.
