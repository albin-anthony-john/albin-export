using Albin.Export.Abstractions;

namespace Albin.Export.Word;

/// <summary>
/// Page size presets shared by the Word and PDF exporters.
/// </summary>
public enum ExportPageSize
{
    /// <summary>ISO A4, 210 x 297 mm.</summary>
    A4 = 0,

    /// <summary>US Letter, 8.5 x 11 in.</summary>
    Letter = 1,

    /// <summary>ISO A3, 297 x 420 mm.</summary>
    A3 = 2,
}

/// <summary>
/// Options for <see cref="WordExporter"/>.
/// </summary>
public sealed class WordExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the body font family. Defaults to <c>Calibri</c>.
    /// </summary>
    public string FontFamily { get; set; } = "Calibri";

    /// <summary>
    /// Gets or sets the body font size in points. Defaults to 10.
    /// </summary>
    public double FontSize { get; set; } = 10d;

    /// <summary>
    /// Gets or sets the page size. Defaults to <see cref="ExportPageSize.A4"/>.
    /// </summary>
    public ExportPageSize PageSize { get; set; } = ExportPageSize.A4;

    /// <summary>
    /// Gets or sets a value indicating whether the page is laid out in landscape orientation.
    /// </summary>
    public bool Landscape { get; set; }

    /// <summary>
    /// Gets or sets the page margin in points, applied to all four edges. Defaults to 56 (about 2 cm).
    /// </summary>
    public double Margin { get; set; } = 56d;

    /// <summary>
    /// Gets or sets the text placed in the page header. When <see langword="null"/> the document
    /// title is used; set an empty string to suppress the header.
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// Gets or sets the text placed in the page footer, to the left of the page number.
    /// When <see langword="null"/> the document footer text is used.
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether "Page X of Y" is written in the footer.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether tables get a light grid and a shaded, repeating header
    /// row. Defaults to <see langword="true"/>.
    /// </summary>
    public bool StyleTables { get; set; } = true;
}
