using System.Globalization;

namespace Albin.Export.Abstractions;

/// <summary>
/// Options shared by every exporter. Format specific packages derive from this type
/// (for example <c>PdfExportOptions</c>) and add their own settings.
/// </summary>
/// <remarks>
/// Instances are intended to be created per export call and treated as immutable afterwards.
/// </remarks>
public class ExportOptions
{
    /// <summary>
    /// Gets the default options instance.
    /// </summary>
    public static ExportOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the requested file name without extension.
    /// </summary>
    /// <remarks>
    /// The value is sanitized before it is used: directory separators, drive qualifiers, reserved
    /// device names and control characters are removed so an export can never target an arbitrary
    /// filesystem path. When <see langword="null"/> the exporter derives a name from the document title.
    /// </remarks>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether column headers are written for tabular formats.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the culture used to format dates and numbers.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/> so output is stable across servers.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the format string applied to <see cref="DateTime"/> and <see cref="DateTimeOffset"/>
    /// values when a column does not define its own format. Defaults to <c>"yyyy-MM-dd HH:mm:ss"</c>.
    /// </summary>
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Gets or sets the text written in place of <see langword="null"/> cell values. Defaults to an empty string.
    /// </summary>
    public string NullText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional author recorded in document metadata where the format supports it.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Creates a shallow copy of these options.
    /// </summary>
    /// <returns>A copy that can be mutated without affecting the original.</returns>
    public virtual ExportOptions Clone() => (ExportOptions)MemberwiseClone();
}
