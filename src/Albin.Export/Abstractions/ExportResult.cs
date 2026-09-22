namespace Albin.Export.Abstractions;

/// <summary>
/// The outcome of an export: a stream plus the metadata a caller needs to return it to a client.
/// </summary>
/// <remarks>
/// <para>
/// The result owns <see cref="Stream"/>. Dispose the result (or the stream) once the content has
/// been consumed. ASP.NET Core's <c>File(...)</c> result and <c>Results.File(...)</c> dispose the
/// stream for you, so a controller can simply return it:
/// </para>
/// <code>
/// var result = await exportService.ExportAsync(data, ExportFormat.Pdf, options, cancellationToken);
/// return File(result.Stream, result.ContentType, result.FileName);
/// </code>
/// <para>
/// The stream is positioned at zero and ready to read.
/// </para>
/// </remarks>
public sealed class ExportResult : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportResult"/> class.
    /// </summary>
    /// <param name="fileName">The file name including extension. It is sanitized before use.</param>
    /// <param name="contentType">The media type of the content.</param>
    /// <param name="stream">The readable content stream. Ownership transfers to the result.</param>
    /// <param name="format">The format that produced the content.</param>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> or <paramref name="contentType"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public ExportResult(string fileName, string contentType, Stream stream, ExportFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(stream);

        FileName = ExportFileName.Sanitize(fileName);
        ContentType = contentType;
        Format = format;
        _stream = stream;
    }

    /// <summary>Gets the sanitized file name including extension.</summary>
    public string FileName { get; }

    /// <summary>Gets the media type, for example <c>application/pdf</c>.</summary>
    public string ContentType { get; }

    /// <summary>Gets the format that produced the content.</summary>
    public ExportFormat Format { get; }

    /// <summary>
    /// Gets the content stream, positioned at the beginning.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The result has been disposed.</exception>
    public Stream Stream
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _stream;
        }
    }

    /// <summary>
    /// Gets the content length in bytes when the stream is seekable, otherwise <see langword="null"/>.
    /// </summary>
    public long? Length => !_disposed && _stream.CanSeek ? _stream.Length : null;

    /// <summary>
    /// Copies the content to a destination stream without buffering it again.
    /// </summary>
    /// <param name="destination">The destination stream, for example the HTTP response body.</param>
    /// <param name="cancellationToken">Token used to cancel the copy.</param>
    /// <returns>A task that completes when the content has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stream.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Reads the whole content into a byte array.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The content bytes.</returns>
    /// <remarks>Prefer <see cref="CopyToAsync"/> or returning <see cref="Stream"/> for large exports.</remarks>
    public async ValueTask<byte[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stream is MemoryStream memory && memory.TryGetBuffer(out var buffer))
        {
            return [.. buffer];
        }

        var capacity = (int)Math.Clamp(Length ?? 0L, 0L, int.MaxValue);
        using var copy = new MemoryStream(capacity);
        await _stream.CopyToAsync(copy, cancellationToken);
        return copy.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stream.DisposeAsync();
    }
}
