using System.IO.Compression;
using Albin.Export.Abstractions;

namespace Albin.Export.Zip;

/// <summary>
/// Packages several exports into a single ZIP archive using <see cref="System.IO.Compression"/>.
/// </summary>
/// <remarks>
/// <para>
/// Entry content is streamed into the archive one entry at a time, so bundling a set of large reports
/// does not require holding them all in memory. Entry names are sanitized, so a name carrying
/// <c>../</c> or an absolute path cannot escape the archive root.
/// </para>
/// <para>
/// The usual way to use this is the <c>ExportBundleAsync</c> extension on
/// <see cref="IExportService"/>, which exports the same data in several formats and zips the results.
/// Use the exporter directly through <see cref="ZipExportOptions.Entries"/> when you need full
/// control over the archive layout.
/// </para>
/// </remarks>
public sealed class ZipExporter : IExporter
{
    /// <inheritdoc />
    public ExportFormat Format => ExportFormat.Zip;

    /// <inheritdoc />
    public string ContentType => ExportContentTypes.Zip;

    /// <inheritdoc />
    public string FileExtension => ".zip";

    /// <inheritdoc />
    public Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        var options = request.Options as ZipExportOptions
            ?? throw new ExportException(
                "ZIP export needs ZipExportOptions with at least one entry. Use "
                + "IExportService.ExportBundleAsync(...) to bundle several formats of the same data.");

        return WriteAsync(destination, options.Entries, options.CompressionLevel, cancellationToken);
    }

    /// <summary>
    /// Writes a ZIP archive containing the supplied entries.
    /// </summary>
    /// <param name="destination">The stream that receives the archive; it is not disposed.</param>
    /// <param name="entries">The entries to add. Duplicate names get a numeric suffix.</param>
    /// <param name="compressionLevel">The compression level.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the archive has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="entries"/> is <see langword="null"/>.</exception>
    public static Task WriteAsync(
        Stream destination,
        IEnumerable<ZipEntrySource> entries,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entries);

        var materialized = entries.ToArray();

        // ZipArchive writes the central directory synchronously when it is disposed, so for a
        // non-seekable destination (an HTTP response body) the archive is built in a buffer first.
        return ExportStreams.WriteSeekableAsync(
            destination,
            (stream, token) => WriteArchiveAsync(stream, materialized, compressionLevel, token),
            capacityHint: 64 * 1024,
            cancellationToken);
    }

    private static async Task WriteArchiveAsync(
        Stream destination,
        ZipEntrySource[] entries,
        CompressionLevel compressionLevel,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = UniqueName(entry.Name, used);
            var archiveEntry = archive.CreateEntry(name, compressionLevel);

            var entryStream = archiveEntry.Open();
            await using (entryStream)
            {
                await entry.WriteContentAsync(entryStream, cancellationToken);
                await entryStream.FlushAsync(cancellationToken);
            }
        }
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var stem = extension.Length > 0 ? name[..^extension.Length] : name;

        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            var candidate = $"{stem} ({suffix}){extension}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }

        var fallback = $"{stem}-{Guid.NewGuid():N}{extension}";
        used.Add(fallback);
        return fallback;
    }
}
