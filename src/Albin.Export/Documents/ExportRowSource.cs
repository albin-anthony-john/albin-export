using System.Runtime.CompilerServices;

namespace Albin.Export.Documents;

/// <summary>
/// Factory methods for the built-in <see cref="IExportRowSource"/> implementations.
/// </summary>
public static class ExportRowSource
{
    /// <summary>
    /// Gets a row source that yields no rows.
    /// </summary>
    public static IExportRowSource Empty { get; } = new EmptyRowSource();

    /// <summary>
    /// Creates a row source over pre-materialized cell arrays.
    /// </summary>
    /// <param name="rows">The rows. Each array holds the cells of one row.</param>
    /// <returns>A streaming row source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is <see langword="null"/>.</exception>
    public static IExportRowSource FromCells(IEnumerable<object?[]> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new CellsRowSource(rows);
    }

    /// <summary>
    /// Creates a row source that projects each item of a sequence into cells using the supplied accessors.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="accessors">One value accessor per column.</param>
    /// <returns>A streaming, allocation-light row source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="accessors"/> is <see langword="null"/>.</exception>
    public static IExportRowSource FromItems<T>(IEnumerable<T> items, IReadOnlyList<Func<T, object?>> accessors)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(accessors);
        return new ItemsRowSource<T>(items, accessors);
    }

    /// <summary>
    /// Creates a row source that projects each item of an asynchronous sequence into cells.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to export.</param>
    /// <param name="accessors">One value accessor per column.</param>
    /// <param name="count">Optional known row count used as a sizing hint.</param>
    /// <returns>A streaming, allocation-light row source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="accessors"/> is <see langword="null"/>.</exception>
    public static IExportRowSource FromAsyncItems<T>(
        IAsyncEnumerable<T> items,
        IReadOnlyList<Func<T, object?>> accessors,
        int? count = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(accessors);
        return new AsyncItemsRowSource<T>(items, accessors, count);
    }

    private sealed class EmptyRowSource : IExportRowSource
    {
        public int? Count => 0;

        public IAsyncEnumerable<ExportRow> ReadAsync(CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<ExportRow>();
    }

    private sealed class CellsRowSource(IEnumerable<object?[]> rows) : IExportRowSource
    {
        public int? Count => rows.TryGetNonEnumeratedCount(out var count) ? count : null;

        public async IAsyncEnumerable<ExportRow> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ExportRow(row ?? []);
            }

            await ValueTask.CompletedTask;
        }
    }

    private sealed class ItemsRowSource<T>(IEnumerable<T> items, IReadOnlyList<Func<T, object?>> accessors)
        : IExportRowSource
    {
        public int? Count => items.TryGetNonEnumeratedCount(out var count) ? count : null;

        public async IAsyncEnumerable<ExportRow> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new object?[accessors.Count];
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = accessors[i](item);
                }

                yield return new ExportRow(buffer);
            }

            await ValueTask.CompletedTask;
        }
    }

    private sealed class AsyncItemsRowSource<T>(
        IAsyncEnumerable<T> items,
        IReadOnlyList<Func<T, object?>> accessors,
        int? count) : IExportRowSource
    {
        public int? Count => count;

        public async IAsyncEnumerable<ExportRow> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new object?[accessors.Count];
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = accessors[i](item);
                }

                yield return new ExportRow(buffer);
            }
        }
    }
}
