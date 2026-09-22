using Albin.Export.Abstractions;

namespace Albin.Export.Csv;

/// <summary>
/// Options for <see cref="CsvExporter"/>.
/// </summary>
public sealed class CsvExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the field delimiter. Defaults to <c>,</c>.
    /// Use <c>;</c> for locales where Excel expects a semicolon.
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Gets or sets the quote character. Defaults to <c>"</c>.
    /// </summary>
    public char Quote { get; set; } = '"';

    /// <summary>
    /// Gets or sets the line ending. Defaults to <c>\r\n</c>, which is what RFC 4180 specifies.
    /// </summary>
    public string NewLine { get; set; } = "\r\n";

    /// <summary>
    /// Gets or sets a value indicating whether a UTF-8 byte order mark is written.
    /// Defaults to <see langword="true"/> so Excel detects the encoding and shows non-ASCII text correctly.
    /// </summary>
    public bool WriteByteOrderMark { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether values that a spreadsheet would treat as a formula
    /// (those starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return) are escaped.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// This mitigates CSV injection, where exported user content is executed when the file is opened
    /// in Excel. Only disable it when the destination is a machine-read pipeline.
    /// </remarks>
    public bool EscapeFormulas { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether every table in the document is written, separated by a
    /// blank line. Defaults to <see langword="false"/>, which writes only the first table because CSV
    /// has no concept of multiple sheets.
    /// </summary>
    public bool IncludeAllTables { get; set; }
}
