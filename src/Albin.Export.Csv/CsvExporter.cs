using Albin.Export.Abstractions;
using Albin.Export.Documents;
using CsvHelper;
using CsvHelper.Configuration;

namespace Albin.Export.Csv;

/// <summary>
/// Exports the tables of a document as CSV using CsvHelper.
/// </summary>
/// <remarks>
/// Rows are pulled from the row source and written straight through the output stream, so memory use
/// stays flat no matter how many rows are exported. Quoting, escaping and delimiters follow RFC 4180
/// via CsvHelper, and formula-injection payloads are escaped by default
/// (see <see cref="CsvExportOptions.EscapeFormulas"/>).
/// </remarks>
public sealed class CsvExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Csv;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Csv;

    /// <inheritdoc />
    public string FileExtension => ".csv";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as CsvExportOptions ?? new CsvExportOptions();
        var tables = options.IncludeAllTables
            ? [.. request.Document.Tables()]
            : request.Document.Tables().Take(1).ToArray();

        var writer = ExportStreams.CreateWriter(destination, options.WriteByteOrderMark);
        await using (writer)
        {
            var csv = new CsvWriter(writer, CreateConfiguration(options), leaveOpen: true);
            await using (csv)
            {
                for (var index = 0; index < tables.Length; index++)
                {
                    if (index > 0)
                    {
                        await csv.NextRecordAsync();
                    }

                    await WriteTableAsync(csv, tables[index], options, cancellationToken);
                }

                await csv.FlushAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }
    }

    private static async Task WriteTableAsync(
        CsvWriter csv,
        ExportTable table,
        CsvExportOptions options,
        CancellationToken cancellationToken)
    {
        if (options.IncludeHeaders)
        {
            foreach (var column in table.Columns)
            {
                csv.WriteField(column.Header);
            }

            await csv.NextRecordAsync();
        }

        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < row.FieldCount ? row[i] : null;
                csv.WriteField(ExportValueFormatter.Format(value, table.Columns[i], options));
            }

            await csv.NextRecordAsync();
        }

        if (table.TotalsRow is { Count: > 0 } totals)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < totals.Count ? totals[i] : null;
                csv.WriteField(ExportValueFormatter.Format(value, table.Columns[i], options));
            }

            await csv.NextRecordAsync();
        }
    }

    private static CsvConfiguration CreateConfiguration(CsvExportOptions options) =>
        new(options.Culture)
        {
            Delimiter = string.IsNullOrEmpty(options.Delimiter) ? "," : options.Delimiter,
            Quote = options.Quote,
            NewLine = options.NewLine,
            HasHeaderRecord = options.IncludeHeaders,
            InjectionOptions = options.EscapeFormulas ? InjectionOptions.Escape : InjectionOptions.None,
            Mode = CsvMode.RFC4180,
            ShouldQuote = args => ShouldQuote(args.Field, options),
        };

    private static bool ShouldQuote(string? field, CsvExportOptions options)
    {
        if (string.IsNullOrEmpty(field))
        {
            return false;
        }

        return field.Contains(options.Quote)
            || field.Contains('\n')
            || field.Contains('\r')
            || (!string.IsNullOrEmpty(options.Delimiter) && field.Contains(options.Delimiter, StringComparison.Ordinal));
    }
}
