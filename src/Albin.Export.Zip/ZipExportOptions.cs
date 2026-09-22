using System.IO.Compression;
using Albin.Export.Abstractions;

namespace Albin.Export.Zip;

/// <summary>
/// Options for <see cref="ZipExporter"/>.
/// </summary>
public sealed class ZipExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the entries to place in the archive.
    /// </summary>
    public IReadOnlyList<ZipEntrySource> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the compression level. Defaults to <see cref="CompressionLevel.Optimal"/>.
    /// Use <see cref="CompressionLevel.Fastest"/> for large archives served over a fast network.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
}
