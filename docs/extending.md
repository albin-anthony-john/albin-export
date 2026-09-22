# Custom exporters

Implement `IExporter` and register it. The service is a dictionary of format → exporter; the last registration for a format wins.

```csharp
public sealed class TsvExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Csv; // or introduce your own if you fork the enum
    public string ContentType => "text/tab-separated-values; charset=utf-8";
    public string FileExtension => ".tsv";

    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var writer = ExportStreams.CreateWriter(destination);
        await using (writer.ConfigureAwait(false))
        {
            foreach (var table in request.Document.Tables())
            {
                await foreach (var row in table.Rows.ReadAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // write cells...
                }
            }

            await writer.FlushAsync(cancellationToken);
        }
    }
}
```

Rules:

1. Do **not** dispose `destination`.
2. Honour `cancellationToken`.
3. Be thread-safe (the exporter is a singleton).
4. Prefer writing through the stream; if you need seek, call `ExportStreams.WriteSeekableAsync`.
5. Sanitize any file name you produce with `ExportFileName.Sanitize`.

Registration:

```csharp
builder.Services.AddAlbinExport(export => export
    .AddCsv()
    .AddExporter<TsvExporter>()); // replaces CsvExporter if Format is Csv
```

Or pass an instance:

```csharp
export.AddExporter(new TsvExporter());
```

For a brand-new format, add a value to `ExportFormat` in a future major version, or handle it entirely outside `IExportService` by injecting your exporter directly. Keeping the enum closed avoids apps depending on unregistered values.

## Testing a custom exporter

The test project constructs `ExportService` with explicit exporters and writes to `MemoryStream` / a write-only stream. Follow that pattern: empty data, unicode, cancellation, and "does not dispose the destination".
