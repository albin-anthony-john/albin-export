using Albin.Export.Documents;

namespace Albin.Export.Abstractions;

/// <summary>
/// Everything an exporter needs: the document to render, the raw payload for object formats and
/// the options to apply.
/// </summary>
/// <remarks>
/// <para>
/// The request deliberately carries two views of the same data. <see cref="Document"/> is the
/// presentation view used by PDF, Excel, CSV, HTML, Word, Markdown and chart output.
/// <see cref="Data"/> is the original object graph, which JSON and XML serialize directly so that
/// nesting and types survive.
/// </para>
/// <para>
/// A request holds no filesystem paths and never touches a data store. It is created from
/// already-prepared DTOs by the calling application.
/// </para>
/// </remarks>
public sealed class ExportRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRequest"/> class.
    /// </summary>
    /// <param name="document">The presentation view of the data.</param>
    /// <param name="options">The export options. Defaults to <see cref="ExportOptions.Default"/>.</param>
    /// <param name="data">
    /// The original payload used by object formats (JSON, XML). When <see langword="null"/> those
    /// exporters project <paramref name="document"/> instead.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public ExportRequest(ExportDocument document, ExportOptions? options = null, object? data = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;
        Options = options ?? ExportOptions.Default;
        Data = data;
    }

    /// <summary>Gets the presentation view of the data.</summary>
    public ExportDocument Document { get; }

    /// <summary>Gets the export options.</summary>
    public ExportOptions Options { get; }

    /// <summary>Gets the original payload for object formats, or <see langword="null"/>.</summary>
    public object? Data { get; }

    /// <summary>
    /// Gets the first table of <see cref="Document"/>, or <see langword="null"/> when the document has none.
    /// </summary>
    public ExportTable? PrimaryTable => Document.Tables().FirstOrDefault();

    /// <summary>
    /// Gets the file name stem to use for the produced file, without extension.
    /// </summary>
    /// <returns>
    /// The sanitized <see cref="ExportOptions.FileName"/> when set, otherwise a name derived from the
    /// document title, table name or the literal <c>export</c>.
    /// </returns>
    public string ResolveFileNameStem()
        => ExportFileName.Sanitize(Options.FileName ?? Document.Title ?? PrimaryTable?.Name ?? "export");

    /// <summary>
    /// Creates a request for a sequence of DTOs, deriving the table columns from the item type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The already-prepared items to export.</param>
    /// <param name="options">The export options.</param>
    /// <param name="title">The document title. Defaults to the type name.</param>
    /// <returns>The created request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static ExportRequest FromItems<T>(IEnumerable<T> items, ExportOptions? options = null, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new ExportRequest(ExportDocument.FromItems(items, title), options, items);
    }

    /// <summary>
    /// Creates a request for a single object. Object formats serialize it as-is; tabular formats
    /// render a one-row table.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="data">The payload.</param>
    /// <param name="options">The export options.</param>
    /// <param name="title">The document title.</param>
    /// <returns>The created request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static ExportRequest FromObject<T>(T data, ExportOptions? options = null, string? title = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ExportRequest(ExportDocument.FromItems<T>([data], title), options, data);
    }

    /// <summary>
    /// Creates a request with no document, for exporters whose content comes entirely from the
    /// options — currently ZIP, whose entries live on <c>ZipExportOptions.Entries</c>.
    /// </summary>
    /// <param name="options">The export options carrying the content.</param>
    /// <returns>The created request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static ExportRequest Empty(ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ExportRequest(new ExportDocument(), options);
    }

    /// <summary>
    /// Creates a request for a pre-built document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The export options.</param>
    /// <param name="data">The optional payload for object formats.</param>
    /// <returns>The created request.</returns>
    public static ExportRequest FromDocument(ExportDocument document, ExportOptions? options = null, object? data = null)
        => new(document, options, data);

    /// <summary>
    /// Returns a copy of this request with different options.
    /// </summary>
    /// <param name="options">The replacement options.</param>
    /// <returns>A new request instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ExportRequest WithOptions(ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ExportRequest(Document, options, Data);
    }
}
