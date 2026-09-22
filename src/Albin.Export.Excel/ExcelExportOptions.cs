using Albin.Export.Abstractions;

namespace Albin.Export.Excel;

/// <summary>
/// Options for <see cref="ExcelExporter"/>.
/// </summary>
public sealed class ExcelExportOptions : ExportOptions
{
    /// <summary>The largest number of data rows a worksheet can hold (the Excel limit minus the header row).</summary>
    public const int MaxRowsPerWorksheet = 1_048_575;

    /// <summary>
    /// Gets or sets a value indicating whether the header row is styled (bold on a light fill).
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool StyleHeaderRow { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether column widths are fitted to their content.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Fitting inspects every cell, which is the single most expensive part of writing a large sheet.
    /// Turn it off (or set <see cref="Documents.ExportColumn.Width"/> per column) for exports with
    /// hundreds of thousands of rows.
    /// </remarks>
    public bool AutoFitColumns { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum width, in characters, that <see cref="AutoFitColumns"/> may produce.
    /// Defaults to 60.
    /// </summary>
    public double MaxColumnWidth { get; set; } = 60d;

    /// <summary>
    /// Gets or sets the Excel number format applied to numeric columns without their own format.
    /// Defaults to <c>#,##0.###</c>.
    /// </summary>
    public string DefaultNumberFormat { get; set; } = "#,##0.###";

    /// <summary>
    /// Gets or sets the Excel number format applied to date columns without their own format.
    /// Defaults to <c>yyyy-mm-dd hh:mm</c>.
    /// </summary>
    public string DefaultDateFormat { get; set; } = "yyyy-mm-dd hh:mm";

    /// <summary>
    /// Gets or sets a value indicating whether values are written as typed cells (numbers, dates and
    /// booleans) rather than text. Defaults to <see langword="true"/>.
    /// </summary>
    public bool TypedCells { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether tables longer than
    /// <see cref="MaxRowsPerWorksheet"/> continue on additional worksheets.
    /// Defaults to <see langword="true"/>. When disabled an <see cref="ExportException"/> is thrown instead.
    /// </summary>
    public bool SplitLargeTables { get; set; } = true;
}
