using System.Globalization;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export;

/// <summary>
/// Converts raw cell values into display text using the column format and the export culture.
/// </summary>
/// <remarks>
/// Every text-producing exporter routes cell values through this class so CSV, HTML, Markdown, PDF
/// and Word all render the same value identically.
/// </remarks>
public static class ExportValueFormatter
{
    /// <summary>
    /// Formats a cell value.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="column">The column the value belongs to, used for its format string.</param>
    /// <param name="options">The export options supplying culture, date format and null text.</param>
    /// <returns>The display text. Never <see langword="null"/>.</returns>
    public static string Format(object? value, ExportColumn? column, ExportOptions? options = null)
    {
        options ??= ExportOptions.Default;
        var culture = options.Culture;

        return value switch
        {
            null or DBNull => options.NullText,
            string text => text,
            bool flag => flag ? bool.TrueString : bool.FalseString,
            DateTime dateTime => dateTime.ToString(column?.Format ?? options.DateTimeFormat, culture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(column?.Format ?? options.DateTimeFormat, culture),
            byte[] bytes => string.Format(culture, "[{0} bytes]", bytes.Length),
            IFormattable formattable => formattable.ToString(column?.Format, culture),
            IEnumerable<string> strings => string.Join(", ", strings),
            System.Collections.IEnumerable sequence => string.Join(", ", Enumerate(sequence, column, options)),
            _ => Convert.ToString(value, culture) ?? options.NullText,
        };
    }

    /// <summary>
    /// Converts a value to the number, date or text primitive best suited for typed cells (Excel).
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>
    /// A <see cref="double"/>, <see cref="DateTime"/>, <see cref="bool"/> or <see cref="string"/>,
    /// or <see langword="null"/> when the value is empty.
    /// </returns>
    public static object? ToPrimitive(object? value) => value switch
    {
        null or DBNull => null,
        string or bool or double or DateTime => value,
        decimal d => (double)d,
        float f => (double)f,
        byte or sbyte or short or ushort or int or uint or long or ulong =>
            Convert.ToDouble(value, CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.UtcDateTime,
        DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
        TimeOnly timeOnly => DateTime.MinValue.Add(timeOnly.ToTimeSpan()),
        Enum e => e.ToString(),
        _ => null,
    };

    private static IEnumerable<string> Enumerate(
        System.Collections.IEnumerable sequence,
        ExportColumn? column,
        ExportOptions options)
    {
        foreach (var item in sequence)
        {
            yield return Format(item, column, options);
        }
    }
}
