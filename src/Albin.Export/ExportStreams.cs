using System.Text;

namespace Albin.Export;

/// <summary>
/// Stream helpers shared by the exporter packages.
/// </summary>
public static class ExportStreams
{
    /// <summary>The buffer size used for stream copies (80 KB, matching the runtime default).</summary>
    public const int CopyBufferSize = 81920;

    /// <summary>The buffer size used by the text writers (16 KB).</summary>
    public const int TextBufferSize = 16 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Creates a UTF-8 <see cref="StreamWriter"/> over <paramref name="destination"/> that leaves the
    /// stream open when disposed.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="byteOrderMark">
    /// Whether to emit a UTF-8 byte order mark. Useful for CSV opened in Excel, undesirable elsewhere.
    /// </param>
    /// <returns>A writer that must be disposed (or flushed) by the caller.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static StreamWriter CreateWriter(Stream destination, bool byteOrderMark = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new StreamWriter(
            destination,
            byteOrderMark ? Utf8WithBom : Utf8NoBom,
            TextBufferSize,
            leaveOpen: true);
    }

    /// <summary>
    /// Writes UTF-8 text to a stream without taking ownership of it.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="byteOrderMark">Whether to emit a UTF-8 byte order mark.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the text has been flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public static async Task WriteTextAsync(
        Stream destination,
        string text,
        bool byteOrderMark = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(text);

        var writer = CreateWriter(destination, byteOrderMark);
        await using (writer)
        {
            await writer.WriteAsync(text.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Creates an in-memory buffer with a capacity hint.
    /// </summary>
    /// <param name="capacityHint">The expected size in bytes. Negative values are ignored.</param>
    /// <returns>A writable, seekable stream.</returns>
    public static MemoryStream CreateBuffer(int capacityHint = 0)
        => capacityHint > 0 ? new MemoryStream(capacityHint) : new MemoryStream();

    /// <summary>
    /// Runs a synchronous writer that needs a readable, seekable stream (the Office formats and PDF
    /// all do) against <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The final destination stream.</param>
    /// <param name="write">The callback that writes the content.</param>
    /// <param name="capacityHint">Capacity hint used when an intermediate buffer is required.</param>
    /// <param name="cancellationToken">Token used to cancel before and after the write.</param>
    /// <returns>A task that completes when the content has reached <paramref name="destination"/>.</returns>
    /// <remarks>
    /// When the destination already supports seeking and reading (for example the in-memory buffer used
    /// by <see cref="Abstractions.IExportService.ExportAsync(Abstractions.ExportRequest, Abstractions.ExportFormat, CancellationToken)"/>)
    /// the content is written in place and no copy happens. Otherwise, for example when writing straight
    /// to an HTTP response body, a single intermediate buffer is used.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="write"/> is <see langword="null"/>.</exception>
    public static async Task WriteSeekableAsync(
        Stream destination,
        Action<Stream> write,
        int capacityHint = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(write);
        cancellationToken.ThrowIfCancellationRequested();

        if (destination is { CanSeek: true, CanRead: true, CanWrite: true })
        {
            write(destination);
            return;
        }

        using var buffer = CreateBuffer(capacityHint);
        write(buffer);
        cancellationToken.ThrowIfCancellationRequested();
        buffer.Position = 0;
        await buffer.CopyToAsync(destination, CopyBufferSize, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous writer that needs a readable, seekable stream against
    /// <paramref name="destination"/>, buffering only when the destination cannot provide one.
    /// </summary>
    /// <param name="destination">The final destination stream.</param>
    /// <param name="write">The callback that writes the content.</param>
    /// <param name="capacityHint">Capacity hint used when an intermediate buffer is required.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the content has reached <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="write"/> is <see langword="null"/>.</exception>
    public static async Task WriteSeekableAsync(
        Stream destination,
        Func<Stream, CancellationToken, Task> write,
        int capacityHint = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(write);
        cancellationToken.ThrowIfCancellationRequested();

        if (destination is { CanSeek: true, CanRead: true, CanWrite: true })
        {
            await write(destination, cancellationToken);
            return;
        }

        var buffer = CreateBuffer(capacityHint);
        await using (buffer)
        {
            await write(buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Position = 0;
            await buffer.CopyToAsync(destination, CopyBufferSize, cancellationToken);
        }
    }
}
