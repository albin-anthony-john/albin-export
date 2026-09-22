using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Xml;

/// <summary>
/// Exports data as UTF-8 XML using the built-in .NET XML writers.
/// </summary>
/// <remarks>
/// <para>
/// The exporter only ever <em>writes</em> XML. It never parses caller supplied markup, so the classic
/// XML attack surface (external entities, DTD expansion, XInclude, schema resolution) does not apply.
/// The writer is created with <see cref="XmlWriterSettings.CheckCharacters"/> enabled and no resolver,
/// and element names derived from column headers are encoded with
/// <see cref="XmlConvert.EncodeLocalName(string)"/> so arbitrary header text can never produce
/// malformed or injected markup.
/// </para>
/// <para>
/// Set <see cref="XmlExportOptions.UseXmlSerializer"/> to serialize your DTOs with
/// <see cref="XmlSerializer"/> instead of writing the tabular projection.
/// </para>
/// </remarks>
public sealed class XmlExporter : IExporter
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> Serializers = new();

    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Xml;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Xml;

    /// <inheritdoc />
    public string FileExtension => ".xml";

    /// <inheritdoc />
    public async Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as XmlExportOptions ?? new XmlExportOptions();

        if (options.UseXmlSerializer && request.Data is not null)
        {
            await WriteWithSerializerAsync(request.Data, options, destination, cancellationToken);
            return;
        }

        await WriteDocumentAsync(request, options, destination, cancellationToken);
    }

    private static async Task WriteDocumentAsync(
        ExportRequest request,
        XmlExportOptions options,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var writer = XmlWriter.Create(destination, CreateSettings(options, async: true));
        await using (writer)
        {
            if (!options.OmitXmlDeclaration)
            {
                await writer.WriteStartDocumentAsync();
            }

            await writer.WriteStartElementAsync(null, SafeName(options.RootElementName, "export"), null)
                ;

            if (!string.IsNullOrWhiteSpace(request.Document.Title))
            {
                await writer.WriteAttributeStringAsync(null, "title", null, request.Document.Title)
                    ;
            }

            foreach (var table in request.Document.Tables())
            {
                await WriteTableAsync(writer, table, options, cancellationToken);
            }

            await writer.WriteEndElementAsync();
            await writer.FlushAsync();
        }
    }

    private static async Task WriteTableAsync(
        XmlWriter writer,
        ExportTable table,
        XmlExportOptions options,
        CancellationToken cancellationToken)
    {
        var tableName = SafeName(table.Name, "table");
        var rowName = SafeName(options.RowElementName, "row");
        var columnNames = new string[table.Columns.Count];
        for (var i = 0; i < columnNames.Length; i++)
        {
            columnNames[i] = SafeName(table.Columns[i].Header, "column" + (i + 1).ToString(options.Culture));
        }

        await writer.WriteStartElementAsync(null, tableName, null);

        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            await writer.WriteStartElementAsync(null, rowName, null);
            for (var i = 0; i < columnNames.Length; i++)
            {
                var value = i < row.FieldCount ? row[i] : null;
                var text = ExportValueFormatter.Format(value, table.Columns[i], options);
                await writer.WriteElementStringAsync(null, columnNames[i], null, text);
            }

            await writer.WriteEndElementAsync();
        }

        await writer.WriteEndElementAsync();
    }

    private static Task WriteWithSerializerAsync(
        object data,
        XmlExportOptions options,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var serializer = Serializers.GetOrAdd(data.GetType(), static type => new XmlSerializer(type));
        var settings = CreateSettings(options, async: false);

        return ExportStreams.WriteSeekableAsync(
            destination,
            stream =>
            {
                using var writer = XmlWriter.Create(stream, settings);
                serializer.Serialize(writer, data);
                writer.Flush();
            },
            capacityHint: 64 * 1024,
            cancellationToken);
    }

    private static XmlWriterSettings CreateSettings(XmlExportOptions options, bool async) => new()
    {
        Async = async,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = options.Indent,
        IndentChars = "  ",
        OmitXmlDeclaration = options.OmitXmlDeclaration,
        CheckCharacters = true,
        CloseOutput = false,
        NewLineHandling = NewLineHandling.Entitize,
    };

    private static string SafeName(string? candidate, string fallback)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        var encoded = XmlConvert.EncodeLocalName(candidate.Trim());
        return string.IsNullOrEmpty(encoded) ? fallback : encoded;
    }
}
