using System.Text.Json;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Json;

/// <summary>
/// Exports data as UTF-8 JSON using <see cref="System.Text.Json"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the request carries the original payload (which it does for
/// <c>ExportRequest.FromItems</c>, <c>FromObject</c> and the generic
/// <c>IExportService.ExportAsync&lt;T&gt;</c> overload) that payload is serialized directly, so
/// nested objects, collections and types survive. Otherwise the document's tables are projected into
/// an array of objects.
/// </para>
/// <para>
/// Serialization goes straight to the destination stream; nothing is buffered as a string, and
/// <see cref="IAsyncEnumerable{T}"/> payloads stream row by row.
/// </para>
/// </remarks>
public sealed class JsonExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Json;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Json;

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as JsonExportOptions;
        var serializerOptions = options?.SerializerOptions ?? JsonExportOptions.DefaultSerializerOptions;
        var useDocument = options?.SerializeDocument == true || request.Data is null;

        if (useDocument)
        {
            var projection = await ExportDocumentProjector
                .ProjectAsync(request.Document, cancellationToken)
                ;

            await JsonSerializer
                .SerializeAsync(destination, projection, projection.GetType(), serializerOptions, cancellationToken)
                ;
            return;
        }

        await JsonSerializer
            .SerializeAsync(destination, request.Data, request.Data!.GetType(), serializerOptions, cancellationToken)
            ;
    }
}
