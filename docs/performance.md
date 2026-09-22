# Performance

## Prefer streams

- `ExportToAsync` / `ExportToResponseAsync` write to the destination without a second copy.
- `ExportResult.Stream` is already positioned at 0. Do not call `ToArrayAsync()` for large files.
- Exporters never dispose the destination stream.

## Pick the right format for the data size

| Size | Good fit | Avoid |
| --- | --- | --- |
| Tens of thousands of rows | CSV, JSON, XML, Markdown, HTML | Fitting every Excel column |
| Hundreds of thousands | CSV / JSON streaming | PDF/Word with full materialization of every cell as a document object |
| Charts | SVG (vector) | PNG at huge resolution |

Excel (ClosedXML) and Word (Open XML) and PDF (MigraDoc) build an object model. Memory grows with cell count. CSV/JSON/HTML/XML/Markdown write row by row.

## Cancellation

Every public export method takes a `CancellationToken`. Row sources honour it on `ReadAsync`. Do not wrap exports in `Task.Run`.

## Large tables

```csharp
var table = ExportTable.Create(asyncRows, "Sales")
    .Column("Id", x => x.Id)
    .Column("Total", x => x.Total, format: "N2")
    .Build();
```

`IAsyncEnumerable<T>` is supported. `ExportTable.Create` compiles accessors once; `FromItems` caches them per type.

## Excel specifically

- `AutoFitColumns = false` is the largest win on wide, tall sheets.
- `TypedCells = true` keeps numbers as numbers (smaller, filterable).
- Split is automatic past 1,048,575 data rows.

## Benchmarks

```bash
dotnet run --project benchmarks/Albin.Export.Benchmarks -c Release
```

The project runs CSV, JSON, XML, Markdown, HTML, Excel, PDF, SVG and PNG against 1,000 and 10,000 rows with BenchmarkDotNet. Use it as a regression check, not as a claim of absolute numbers — they depend on the machine and font set.
