namespace Albin.Export.Documents;

/// <summary>
/// A format-independent description of what should be exported: a title, optional metadata and an
/// ordered list of sections.
/// </summary>
/// <remarks>
/// Every exporter consumes this single model, which is what keeps the output of the different
/// formats consistent. Tabular formats (CSV, Excel) use the tables and ignore prose sections;
/// document formats (PDF, Word, HTML, Markdown) render everything.
/// </remarks>
public sealed class ExportDocument
{
    /// <summary>Gets or sets the document title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets or sets an optional subtitle rendered below the title.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Gets or sets an optional footer text used for page footers where supported.</summary>
    public string? Footer { get; init; }

    /// <summary>Gets or sets the document sections in render order.</summary>
    public IReadOnlyList<ExportSection> Sections { get; init; } = [];

    /// <summary>
    /// Gets the tables contained in <see cref="Sections"/>, in order.
    /// </summary>
    /// <returns>The table sections.</returns>
    public IEnumerable<ExportTable> Tables() => Sections.OfType<ExportTable>();

    /// <summary>
    /// Creates a document that contains a single table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="title">The document title. Defaults to the table name.</param>
    /// <returns>The created document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public static ExportDocument FromTable(ExportTable table, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new ExportDocument { Title = title ?? table.Name, Sections = [table] };
    }

    /// <summary>
    /// Creates a document with an automatically derived table for <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="title">The document title. Defaults to the type name.</param>
    /// <returns>The created document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static ExportDocument FromItems<T>(IEnumerable<T> items, string? title = null)
        => FromTable(ExportTable.FromItems(items, title), title);

    /// <summary>
    /// Starts a fluent document builder.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <returns>A new builder.</returns>
    public static ExportDocumentBuilder Builder(string? title = null) => new(title);
}
