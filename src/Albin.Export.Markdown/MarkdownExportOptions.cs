using Albin.Export.Abstractions;

namespace Albin.Export.Markdown;

/// <summary>
/// Options for <see cref="MarkdownExporter"/>.
/// </summary>
public sealed class MarkdownExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the document title is written as a level 1 heading.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeTitle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether table alignment markers (<c>:---:</c>) are emitted.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeAlignmentMarkers { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether images are inlined as <c>data:</c> URIs, which keeps
    /// the document self-contained. Defaults to <see langword="true"/>. When disabled, images are
    /// written as an italic placeholder instead.
    /// </summary>
    public bool EmbedImages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether charts are rendered as a data table. Defaults to
    /// <see langword="true"/>; Markdown has no chart primitive, so the numbers are written instead.
    /// </summary>
    public bool ChartsAsTables { get; set; } = true;
}
