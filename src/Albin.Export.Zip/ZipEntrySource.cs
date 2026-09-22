using System.Text;
using Albin.Export.Abstractions;

namespace Albin.Export.Zip;

/// <summary>
/// One file to place inside a ZIP archive.
/// </summary>
/// <remarks>
/// Entry names are sanitized with <see cref="ExportFileName.SanitizeEntryName(string?)"/>: forward
/// slashes are kept so you can build folders, but <c>..</c> segments, absolute paths, drive
/// qualifiers and backslashes are removed. An entry can therefore never be written outside the
/// archive root when the archive is later extracted.
/// </remarks>
public sealed class ZipEntrySource
{
    private readonly Func<Stream, CancellationToken, Task> _writer;

    private ZipEntrySource(string name, Func<Stream, CancellationToken, Task> writer)
    {
        Name = ExportFileName.SanitizeEntryName(name);
        _writer = writer;
    }

    /// <summary>Gets the sanitized entry name, using <c>/</c> as the folder separator.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates an entry from a byte buffer.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="content">The content.</param>
    /// <returns>The entry.</returns>
    public static ZipEntrySource FromBytes(string name, ReadOnlyMemory<byte> content)
        => new(name, (stream, token) => stream.WriteAsync(content, token).AsTask());

    /// <summary>
    /// Creates an entry from text.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="content">The text content.</param>
    /// <param name="encoding">The encoding to use. Defaults to UTF-8 without a byte order mark.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static ZipEntrySource FromText(string name, string content, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = (encoding ?? new UTF8Encoding(false)).GetBytes(content);
        return FromBytes(name, bytes);
    }

    /// <summary>
    /// Creates an entry that copies from a stream. The source stream is read from its current
    /// position and is not disposed.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="content">The source stream.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static ZipEntrySource FromStream(string name, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ZipEntrySource(
            name,
            (stream, token) => content.CopyToAsync(stream, ExportStreams.CopyBufferSize, token));
    }

    /// <summary>
    /// Creates an entry from an export result, reusing its file name.
    /// </summary>
    /// <param name="result">The export result. The caller stays responsible for disposing it.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static ZipEntrySource FromResult(ExportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return FromStream(result.FileName, result.Stream);
    }

    /// <summary>
    /// Creates an entry whose content is produced on demand, which avoids materializing it in memory.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="writer">Callback invoked with the entry stream when the archive is written.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public static ZipEntrySource FromWriter(string name, Func<Stream, CancellationToken, Task> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new ZipEntrySource(name, writer);
    }

    /// <summary>
    /// Writes this entry's content into the supplied stream.
    /// </summary>
    /// <param name="destination">The entry stream inside the archive.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the content has been written.</returns>
    internal Task WriteContentAsync(Stream destination, CancellationToken cancellationToken)
        => _writer(destination, cancellationToken);
}
