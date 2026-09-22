using Albin.Export.Documents;

namespace Albin.Export.Excel;

/// <summary>
/// Translates the .NET format strings used by <see cref="ExportColumn.Format"/> into Excel number formats.
/// </summary>
internal static class ExcelNumberFormats
{
    /// <summary>
    /// Resolves the Excel number format for a column, or <see langword="null"/> to leave the cell general.
    /// </summary>
    public static string? Resolve(ExportColumn column, object? value, ExcelExportOptions options)
    {
        var format = column.Format;
        if (!string.IsNullOrWhiteSpace(format))
        {
            var translated = Translate(format!);
            if (translated is not null)
            {
                return translated;
            }
        }

        return value switch
        {
            DateTime or DateOnly or DateTimeOffset => options.DefaultDateFormat,
            double or decimal or float => options.DefaultNumberFormat,
            _ when ExportValueTypes.IsDateOrTime(column.ValueType) => options.DefaultDateFormat,
            _ when column.IsNumeric => options.DefaultNumberFormat,
            _ => null,
        };
    }

    /// <summary>
    /// Maps a standard .NET numeric format specifier, or a custom date pattern, to the Excel equivalent.
    /// Returns <see langword="null"/> when the format has no sensible translation.
    /// </summary>
    private static string? Translate(string format)
    {
        var trimmed = format.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        // Already an Excel custom format such as "#,##0.00" or "0.0%": pass it through untouched.
        if (trimmed.Contains('#', StringComparison.Ordinal))
        {
            return trimmed;
        }

        // Date and time patterns share most of their syntax with Excel; Excel wants lowercase months.
        if (trimmed.IndexOfAny(['y', 'd', 'H', 'h', 's']) >= 0 && trimmed.Length > 1)
        {
            return trimmed.Replace("MM", "mm", StringComparison.Ordinal)
                .Replace("M", "m", StringComparison.Ordinal)
                .Replace("tt", "AM/PM", StringComparison.Ordinal)
                .Replace("fff", "000", StringComparison.Ordinal)
                .Replace("H", "h", StringComparison.Ordinal);
        }

        var specifier = char.ToUpperInvariant(trimmed[0]);
        var digits = trimmed.Length > 1 && int.TryParse(trimmed[1..], out var parsed)
            ? Math.Clamp(parsed, 0, 15)
            : -1;

        return specifier switch
        {
            'N' => "#,##0" + Decimals(digits < 0 ? 2 : digits),
            'F' => "0" + Decimals(digits < 0 ? 2 : digits),
            'C' => "#,##0" + Decimals(digits < 0 ? 2 : digits) + ";[Red]-#,##0" + Decimals(digits < 0 ? 2 : digits),
            'P' => "0" + Decimals(digits < 0 ? 0 : digits) + "%",
            'E' => "0" + Decimals(digits < 0 ? 2 : digits) + "E+00",
            'D' when digits > 0 => new string('0', digits),
            'X' => null,
            _ => null,
        };
    }

    private static string Decimals(int count) => count <= 0 ? string.Empty : "." + new string('0', count);
}
