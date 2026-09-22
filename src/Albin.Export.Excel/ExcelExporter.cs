using Albin.Export.Abstractions;
using Albin.Export.Documents;
using ClosedXML.Excel;

namespace Albin.Export.Excel;

/// <summary>
/// Exports the tables of a document as an Office Open XML workbook (<c>.xlsx</c>) using ClosedXML.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="ExportTable"/> becomes a worksheet, with a styled header row, frozen header,
/// auto-filter, fitted column widths, typed cells and Excel number formats derived from the column
/// format strings. A table's <see cref="ExportTable.TotalsRow"/> becomes a bold footer row and may
/// contain <see cref="ExcelFormula"/> values.
/// </para>
/// <para>
/// ClosedXML builds the workbook in memory, so peak memory grows with the number of cells. For very
/// large exports either stream CSV instead, or reduce the cost by switching
/// <see cref="ExcelExportOptions.AutoFitColumns"/> off. Tables beyond the Excel row limit continue on
/// additional worksheets.
/// </para>
/// </remarks>
public sealed class ExcelExporter : IExporter
{
    private static readonly XLColor HeaderFill = XLColor.FromArgb(0xF6, 0xF8, 0xFA);

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Excel;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Excel;

    /// <inheritdoc />
    public string FileExtension => ".xlsx";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as ExcelExportOptions ?? new ExcelExportOptions();

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = request.Document.Title ?? string.Empty;
        workbook.Properties.Subject = request.Document.Subtitle ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(options.Author))
        {
            workbook.Properties.Author = options.Author;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableIndex = 0;
        var wroteAnySheet = false;

        foreach (var table in request.Document.Tables())
        {
            tableIndex++;
            wroteAnySheet = true;
            await WriteTableAsync(workbook, table, options, usedNames, tableIndex, cancellationToken)
                ;
        }

        if (!wroteAnySheet)
        {
            workbook.Worksheets.Add(UniqueSheetName(request.Document.Title, usedNames, 1));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await ExportStreams
            .WriteSeekableAsync(destination, workbook.SaveAs, capacityHint: 64 * 1024, cancellationToken)
            ;
    }

    private static async Task WriteTableAsync(
        XLWorkbook workbook,
        ExportTable table,
        ExcelExportOptions options,
        HashSet<string> usedNames,
        int tableIndex,
        CancellationToken cancellationToken)
    {
        var worksheet = workbook.Worksheets.Add(UniqueSheetName(table.Name, usedNames, tableIndex));
        var columnCount = table.Columns.Count;
        var row = 1;

        if (options.IncludeHeaders && columnCount > 0)
        {
            for (var c = 0; c < columnCount; c++)
            {
                worksheet.Cell(1, c + 1).Value = table.Columns[c].Header;
            }

            if (options.StyleHeaderRow)
            {
                var header = worksheet.Range(1, 1, 1, Math.Max(columnCount, 1)).Style;
                header.Font.Bold = true;
                header.Fill.BackgroundColor = HeaderFill;
                header.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            row++;
        }

        var headerRows = row - 1;
        var dataRows = 0;
        var sheetPart = 1;

        await foreach (var source in table.Rows.ReadAsync(cancellationToken))
        {
            if (dataRows >= ExcelExportOptions.MaxRowsPerWorksheet)
            {
                if (!options.SplitLargeTables)
                {
                    throw new ExportException(
                        $"Table '{table.Name ?? "(unnamed)"}' exceeds the Excel limit of "
                        + $"{ExcelExportOptions.MaxRowsPerWorksheet} rows per worksheet. Enable "
                        + "ExcelExportOptions.SplitLargeTables or export CSV instead.");
                }

                FinishWorksheet(worksheet, table, options, headerRows, dataRows, columnCount);
                sheetPart++;
                worksheet = workbook.Worksheets.Add(
                    UniqueSheetName($"{table.Name ?? "Sheet"} ({sheetPart})", usedNames, tableIndex));

                row = 1;
                if (options.IncludeHeaders && columnCount > 0)
                {
                    for (var c = 0; c < columnCount; c++)
                    {
                        worksheet.Cell(1, c + 1).Value = table.Columns[c].Header;
                    }

                    worksheet.Range(1, 1, 1, columnCount).Style.Font.Bold = options.StyleHeaderRow;
                    row++;
                }

                dataRows = 0;
            }

            for (var c = 0; c < columnCount; c++)
            {
                var value = c < source.FieldCount ? source[c] : null;
                WriteCell(worksheet.Cell(row, c + 1), value, table.Columns[c], options);
            }

            row++;
            dataRows++;
        }

        if (table.TotalsRow is { Count: > 0 } totals)
        {
            for (var c = 0; c < columnCount; c++)
            {
                var value = c < totals.Count ? totals[c] : null;
                var cell = worksheet.Cell(row, c + 1);
                WriteCell(cell, value, table.Columns[c], options, lastDataRow: row - 1);
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
        }

        FinishWorksheet(worksheet, table, options, headerRows, dataRows, columnCount);
    }

    private static void FinishWorksheet(
        IXLWorksheet worksheet,
        ExportTable table,
        ExcelExportOptions options,
        int headerRows,
        int dataRows,
        int columnCount)
    {
        if (columnCount == 0)
        {
            return;
        }

        if (headerRows > 0 && table.FreezeHeader)
        {
            worksheet.SheetView.FreezeRows(headerRows);
        }

        if (headerRows > 0 && table.AutoFilter && dataRows > 0)
        {
            worksheet.Range(1, 1, headerRows + dataRows, columnCount).SetAutoFilter();
        }

        for (var c = 0; c < columnCount; c++)
        {
            if (table.Columns[c].Width is { } width and > 0)
            {
                worksheet.Column(c + 1).Width = width;
            }
            else if (options.AutoFitColumns)
            {
                worksheet.Column(c + 1).AdjustToContents(1d, options.MaxColumnWidth);
            }
        }
    }

    private static void WriteCell(
        IXLCell cell,
        object? value,
        ExportColumn column,
        ExcelExportOptions options,
        int lastDataRow = 0)
    {
        if (value is ExcelFormula formula)
        {
            cell.FormulaA1 = formula.Expression
                .Replace("{lastRow}", lastDataRow.ToString(options.Culture), StringComparison.Ordinal)
                .Replace("{column}", cell.Address.ColumnLetter, StringComparison.Ordinal);
            return;
        }

        if (!options.TypedCells)
        {
            cell.SetValue(ExportValueFormatter.Format(value, column, options));
            return;
        }

        var primitive = ExportValueFormatter.ToPrimitive(value);
        switch (primitive)
        {
            case null:
                if (value is not null)
                {
                    cell.SetValue(ExportValueFormatter.Format(value, column, options));
                }

                break;
            case double number:
                cell.SetValue(number);
                break;
            case DateTime date:
                cell.SetValue(date);
                break;
            case bool flag:
                cell.SetValue(flag);
                break;
            default:
                // Strings are always written as text, never as formulas, even when they start with '='.
                cell.SetValue(primitive.ToString());
                break;
        }

        var numberFormat = ExcelNumberFormats.Resolve(column, primitive, options);
        if (numberFormat is not null)
        {
            cell.Style.NumberFormat.Format = numberFormat;
        }
    }

    private static string UniqueSheetName(string? candidate, HashSet<string> used, int index)
    {
        var name = Sanitize(candidate) is { Length: > 0 } sanitized ? sanitized : $"Sheet{index}";
        if (used.Add(name))
        {
            return name;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var tail = $" ({suffix})";
            var trimmed = name.Length + tail.Length > 31 ? name[..(31 - tail.Length)] : name;
            var attempt = trimmed + tail;
            if (used.Add(attempt))
            {
                return attempt;
            }
        }

        var fallback = $"Sheet{Guid.NewGuid():N}"[..31];
        used.Add(fallback);
        return fallback;
    }

    private static string Sanitize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[31];
        var length = 0;
        foreach (var c in candidate.Trim())
        {
            if (c is '[' or ']' or ':' or '*' or '?' or '/' or '\\' or '\'' || char.IsControl(c))
            {
                continue;
            }

            buffer[length++] = c;
            if (length == buffer.Length)
            {
                break;
            }
        }

        return new string(buffer[..length]).Trim();
    }
}
