namespace Albin.Export.Documents;

/// <summary>
/// Projects the tables of an <see cref="ExportDocument"/> into plain dictionaries.
/// </summary>
/// <remarks>
/// The object formats (JSON, XML) prefer to serialize the caller's original DTOs. When a request does
/// not carry them, this projector turns the document's tabular view into an equivalent structure so
/// those formats still produce meaningful output.
/// </remarks>
public static class ExportDocumentProjector
{
    /// <summary>
    /// Projects one table into a list of column-keyed dictionaries.
    /// </summary>
    /// <param name="table">The table to project.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>One dictionary per row, holding the raw (unformatted) cell values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public static async ValueTask<List<Dictionary<string, object?>>> ProjectTableAsync(
        ExportTable table,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        var rows = new List<Dictionary<string, object?>>(table.Rows.Count ?? 16);
        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            var item = new Dictionary<string, object?>(table.Columns.Count, StringComparer.Ordinal);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                item[table.Columns[i].Header] = i < row.FieldCount ? row[i] : null;
            }

            rows.Add(item);
        }

        return rows;
    }

    /// <summary>
    /// Projects a whole document.
    /// </summary>
    /// <param name="document">The document to project.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>
    /// A list of row dictionaries when the document holds exactly one table, otherwise an object with
    /// <c>title</c>, <c>subtitle</c> and a <c>tables</c> map keyed by table name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static async ValueTask<object> ProjectAsync(
        ExportDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tables = document.Tables().ToArray();
        if (tables.Length == 1)
        {
            return await ProjectTableAsync(tables[0], cancellationToken);
        }

        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < tables.Length; i++)
        {
            var name = string.IsNullOrWhiteSpace(tables[i].Name) ? $"table{i + 1}" : tables[i].Name!;
            map[name] = await ProjectTableAsync(tables[i], cancellationToken);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = document.Title,
            ["subtitle"] = document.Subtitle,
            ["tables"] = map,
        };
    }
}
