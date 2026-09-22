using Albin.Export.Abstractions;

namespace Albin.Export.Pdf;

/// <summary>Page size presets for PDF output.</summary>
public enum PdfPageSize
{
    /// <summary>ISO A4, 210 x 297 mm.</summary>
    A4 = 0,

    /// <summary>US Letter, 8.5 x 11 in.</summary>
    Letter = 1,

    /// <summary>ISO A3, 297 x 420 mm.</summary>
    A3 = 2,

    /// <summary>ISO A5, 148 x 210 mm.</summary>
    A5 = 3,
}

/// <summary>
/// Options for <see cref="PdfExporter"/>.
/// </summary>
public sealed class PdfExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the font family used for body text. Defaults to <c>Arial</c>, which the
    /// cross-platform font resolver maps to the closest installed sans-serif face.
    /// </summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>
    /// Gets or sets the body font size in points. Defaults to 9.5.
    /// </summary>
    public double FontSize { get; set; } = 9.5d;

    /// <summary>
    /// Gets or sets the page size. Defaults to <see cref="PdfPageSize.A4"/>.
    /// </summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    /// <summary>
    /// Gets or sets a value indicating whether the page is laid out in landscape orientation.
    /// Wide tables usually want this.
    /// </summary>
    public bool Landscape { get; set; }

    /// <summary>Gets or sets the top margin in points. Defaults to 56 (about 2 cm).</summary>
    public double MarginTop { get; set; } = 56d;

    /// <summary>Gets or sets the bottom margin in points. Defaults to 56.</summary>
    public double MarginBottom { get; set; } = 56d;

    /// <summary>Gets or sets the left margin in points. Defaults to 48.</summary>
    public double MarginLeft { get; set; } = 48d;

    /// <summary>Gets or sets the right margin in points. Defaults to 48.</summary>
    public double MarginRight { get; set; } = 48d;

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
    /// Gets or sets a value indicating whether table header rows repeat on every page.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool RepeatTableHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether table rows get alternating background shading.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool StripedTables { get; set; } = true;
}
