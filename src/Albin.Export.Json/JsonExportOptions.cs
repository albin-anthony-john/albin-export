using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albin.Export.Abstractions;

namespace Albin.Export.Json;

/// <summary>
/// Options for <see cref="JsonExporter"/>.
/// </summary>
public sealed class JsonExportOptions : ExportOptions
{
    /// <summary>
    /// The serializer options used when none are supplied: camelCase names, indented output, nulls
    /// kept, and the default (HTML-safe) encoder.
    /// </summary>
    /// <remarks>
    /// The default encoder escapes <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c>, which means exported
    /// JSON stays safe if it is later embedded in an HTML page. Replace it with
    /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> only when you control the consumer.
    /// </remarks>
    public static JsonSerializerOptions DefaultSerializerOptions { get; } = CreateDefault();

    /// <summary>
    /// Gets or sets the serializer options. Defaults to <see cref="DefaultSerializerOptions"/>.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = DefaultSerializerOptions;

    /// <summary>
    /// Gets or sets a value indicating whether the document's tables are serialized instead of
    /// <see cref="ExportRequest.Data"/>. Defaults to <see langword="false"/>, which serializes the
    /// original DTOs so nesting and types survive.
    /// </summary>
    public bool SerializeDocument { get; set; }

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.Default,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
