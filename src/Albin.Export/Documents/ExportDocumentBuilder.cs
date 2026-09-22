namespace Albin.Export.Documents;

/// <summary>
/// Fluent builder for an <see cref="ExportDocument"/>.
/// </summary>
/// <example>
/// <code>
/// var document = ExportDocument.Builder("Sales report")
///     .Subtitle("Q1 2026")
///     .Heading("Summary", 2)
///     .Paragraph("Revenue grew 12% year over year.")
///     .Table(ExportTable.FromItems(rows, "Revenue"))
///     .Chart(chartSpec)
///     .Build();
/// </code>
/// </example>
public sealed class ExportDocumentBuilder
{
    private readonly List<ExportSection> _sections = [];
    private string? _title;
    private string? _subtitle;
    private string? _footer;

    internal ExportDocumentBuilder(string? title) => _title = title;

    /// <summary>Sets the document title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Title(string? title)
    {
        _title = title;
        return this;
    }

    /// <summary>Sets the document subtitle.</summary>
    /// <param name="subtitle">The subtitle.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Subtitle(string? subtitle)
    {
        _subtitle = subtitle;
        return this;
    }

    /// <summary>Sets the page footer text.</summary>
    /// <param name="footer">The footer text.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Footer(string? footer)
    {
        _footer = footer;
        return this;
    }

    /// <summary>Appends a heading.</summary>
    /// <param name="text">The heading text.</param>
    /// <param name="level">The heading level between 1 and 6.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Heading(string text, int level = 2)
    {
        _sections.Add(new ExportHeading(text, level));
        return this;
    }

    /// <summary>Appends a paragraph.</summary>
    /// <param name="text">The paragraph text.</param>
    /// <param name="bold">Whether the text is emphasized.</param>
    /// <param name="muted">Whether the text is rendered in a subdued style.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Paragraph(string text, bool bold = false, bool muted = false)
    {
        _sections.Add(new ExportParagraph(text) { Bold = bold, Muted = muted });
        return this;
    }

    /// <summary>Appends a table.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public ExportDocumentBuilder Table(ExportTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _sections.Add(table);
        return this;
    }

    /// <summary>Appends an automatically derived table for <paramref name="items"/>.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="name">An optional table name.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Table<T>(IEnumerable<T> items, string? name = null)
        => Table(ExportTable.FromItems(items, name));

    /// <summary>Appends a chart.</summary>
    /// <param name="chart">The chart definition.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chart"/> is <see langword="null"/>.</exception>
    public ExportDocumentBuilder Chart(ChartSpec chart)
    {
        _sections.Add(new ExportChart(chart));
        return this;
    }

    /// <summary>Appends an image.</summary>
    /// <param name="data">The encoded image bytes.</param>
    /// <param name="contentType">The image media type.</param>
    /// <param name="altText">Alternative text for accessibility.</param>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder Image(ReadOnlyMemory<byte> data, string contentType = "image/png", string? altText = null)
    {
        _sections.Add(new ExportImage(data, contentType) { AltText = altText });
        return this;
    }

    /// <summary>Appends a page break for paginated formats.</summary>
    /// <returns>The same builder for chaining.</returns>
    public ExportDocumentBuilder PageBreak()
    {
        _sections.Add(ExportPageBreak.Instance);
        return this;
    }

    /// <summary>Appends a custom section.</summary>
    /// <param name="section">The section to append.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="section"/> is <see langword="null"/>.</exception>
    public ExportDocumentBuilder Section(ExportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _sections.Add(section);
        return this;
    }

    /// <summary>Builds the document.</summary>
    /// <returns>The configured <see cref="ExportDocument"/>.</returns>
    public ExportDocument Build() => new()
    {
        Title = _title,
        Subtitle = _subtitle,
        Footer = _footer,
        Sections = [.. _sections],
    };
}
