using System.IO.Compression;
using System.Text;
using Albin.Export.Abstractions;
using Albin.Export.Zip;

namespace Albin.Export.Tests;

public sealed class ZipExporterTests
{
    private readonly ZipExporter _exporter = new();

    [Fact]
    public void Metadata_is_correct()
    {
        Assert.Equal(ExportFormat.Zip, _exporter.Format);
        Assert.Equal(".zip", _exporter.FileExtension);
        Assert.Equal(ExportContentTypes.Zip, _exporter.ContentType);
    }

    [Fact]
    public async Task Packages_every_entry_kind()
    {
        var entries = new[]
        {
            ZipEntrySource.FromText("notes.txt", "hello"),
            ZipEntrySource.FromBytes("data.bin", new byte[] { 1, 2, 3 }),
            ZipEntrySource.FromStream("stream.txt", new MemoryStream("streamed"u8.ToArray())),
            ZipEntrySource.FromWriter("written.txt", async (stream, ct) =>
            {
                await stream.WriteAsync("written"u8.ToArray(), ct);
            }),
        };

        using var archive = await OpenAsync(entries);

        Assert.Equal(4, archive.Entries.Count);
        Assert.Equal("hello", await ReadAsync(archive, "notes.txt"));
        Assert.Equal("streamed", await ReadAsync(archive, "stream.txt"));
        Assert.Equal("written", await ReadAsync(archive, "written.txt"));
        Assert.Equal(3, archive.GetEntry("data.bin")!.Length);
    }

    [Fact]
    public async Task Packages_export_results_and_disposes_them()
    {
        var service = TestData.Service();
        var csv = await service.ExportAsync(TestData.Sales(3), ExportFormat.Csv);
        var json = await service.ExportAsync(TestData.Sales(3), ExportFormat.Json);

        using var archive = await OpenAsync([ZipEntrySource.FromResult(csv), ZipEntrySource.FromResult(json)]);

        Assert.Contains(archive.Entries, e => e.Name.EndsWith(".csv", StringComparison.Ordinal));
        Assert.Contains(archive.Entries, e => e.Name.EndsWith(".json", StringComparison.Ordinal));
        Assert.Contains("North", await ReadAsync(archive, archive.Entries.First(e => e.Name.EndsWith(".csv", StringComparison.Ordinal)).FullName));
    }

    [Theory]
    [InlineData("../../etc/passwd", "etc/passwd")]
    [InlineData("/absolute/path.txt", "absolute/path.txt")]
    [InlineData("C:\\Windows\\System32\\evil.dll", "export/Windows/System32/evil.dll")]
    [InlineData("..\\..\\..\\secret.txt", "secret.txt")]
    [InlineData("folder/../../escape.txt", "folder/escape.txt")]
    [InlineData("a/./b/c.txt", "a/b/c.txt")]
    public async Task Entry_names_can_never_escape_the_archive_root(string requested, string expected)
    {
        using var archive = await OpenAsync([ZipEntrySource.FromText(requested, "x")]);
        var entry = Assert.Single(archive.Entries);

        Assert.Equal(expected, entry.FullName);
        Assert.DoesNotContain("..", entry.FullName, StringComparison.Ordinal);
        Assert.False(Path.IsPathRooted(entry.FullName));

        // Extracting into a directory must stay inside it.
        var root = Path.GetFullPath("extract-root");
        var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
        Assert.StartsWith(root + Path.DirectorySeparatorChar, target, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_names_are_made_unique_instead_of_overwriting()
    {
        var entries = new[]
        {
            ZipEntrySource.FromText("report.csv", "first"),
            ZipEntrySource.FromText("report.csv", "second"),
            ZipEntrySource.FromText("report.csv", "third"),
        };

        using var archive = await OpenAsync(entries);

        Assert.Equal(3, archive.Entries.Count);
        Assert.Equal(3, archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("first", await ReadAsync(archive, "report.csv"));
        Assert.Equal("second", await ReadAsync(archive, "report (2).csv"));
        Assert.Equal("third", await ReadAsync(archive, "report (3).csv"));
    }

    [Fact]
    public async Task Nested_folders_are_preserved()
    {
        using var archive = await OpenAsync([ZipEntrySource.FromText("reports/2026/q1.csv", "x")]);
        Assert.Equal("reports/2026/q1.csv", Assert.Single(archive.Entries).FullName);
    }

    [Fact]
    public async Task Compression_level_is_honoured()
    {
        var text = string.Concat(Enumerable.Repeat("compressible ", 5_000));

        var optimal = await ArchiveBytesAsync([ZipEntrySource.FromText("a.txt", text)], CompressionLevel.Optimal);
        var none = await ArchiveBytesAsync([ZipEntrySource.FromText("a.txt", text)], CompressionLevel.NoCompression);

        Assert.True(optimal.Length < none.Length / 4);
    }

    [Fact]
    public async Task An_empty_archive_is_still_valid()
    {
        using var archive = await OpenAsync([]);
        Assert.Empty(archive.Entries);
    }

    [Fact]
    public async Task Unicode_entry_names_survive_the_round_trip()
    {
        using var archive = await OpenAsync([ZipEntrySource.FromText("отчёт-日本語.txt", "данные")]);
        var entry = Assert.Single(archive.Entries);

        Assert.Equal("отчёт-日本語.txt", entry.FullName);
        Assert.Equal("данные", await ReadAsync(archive, entry.FullName));
    }

    [Fact]
    public async Task Export_bundle_produces_one_entry_per_format()
    {
        var service = TestData.Service();
        var request = ExportRequest.FromItems(TestData.Sales(5), new ExportOptions { FileName = "sales" });

        await using var result = await service.ExportBundleAsync(
            request,
            [ExportFormat.Csv, ExportFormat.Json, ExportFormat.Markdown, ExportFormat.Excel]);

        Assert.Equal("sales.zip", result.FileName);
        Assert.Equal(ExportContentTypes.Zip, result.ContentType);

        using var archive = new ZipArchive(result.Stream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal(4, archive.Entries.Count);
        Assert.Contains("sales.csv", archive.Entries.Select(e => e.FullName));
        Assert.Contains("sales.xlsx", archive.Entries.Select(e => e.FullName));
    }

    [Fact]
    public async Task Export_bundle_rejects_an_empty_format_list()
    {
        var service = TestData.Service();
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExportBundleAsync(TestData.Sales(1), Array.Empty<ExportFormat>()));
    }

    [Fact]
    public async Task Create_archive_names_the_result_safely()
    {
        var service = TestData.Service();
        await using var result = await service.CreateArchiveAsync(
            "../../evil",
            [ZipEntrySource.FromText("a.txt", "x")]);

        Assert.Equal("evil.zip", result.FileName);
    }

    [Fact]
    public async Task Writes_to_a_non_seekable_destination()
    {
        using var inner = new MemoryStream();
        await using (var destination = new WriteOnlyStream(inner))
        {
            await ZipExporter.WriteAsync(destination, [ZipEntrySource.FromText("a.txt", "x")]);
        }

        using var archive = new ZipArchive(new MemoryStream(inner.ToArray()), ZipArchiveMode.Read);
        Assert.Single(archive.Entries);
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ZipExporter.WriteAsync(
                new MemoryStream(),
                [ZipEntrySource.FromText("a.txt", "x")],
                CompressionLevel.Optimal,
                cts.Token));
    }

    [Fact]
    public async Task Rejects_invalid_input()
    {
        Assert.Throws<ArgumentNullException>(() => ZipEntrySource.FromText("a.txt", null!));
        Assert.Throws<ArgumentNullException>(() => ZipEntrySource.FromStream("a", null!));
        Assert.Throws<ArgumentNullException>(() => ZipEntrySource.FromWriter("a", null!));
        Assert.Throws<ArgumentNullException>(() => ZipEntrySource.FromResult(null!));

        // A missing or unusable entry name falls back rather than throwing, so a bundle never fails
        // just because one report had an awkward title.
        Assert.Equal("export", ZipEntrySource.FromText(null!, "x").Name);
        Assert.Equal("export", ZipEntrySource.FromText("   ", "x").Name);

        await Assert.ThrowsAsync<ArgumentNullException>(() => ZipExporter.WriteAsync(null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ZipExporter.WriteAsync(new MemoryStream(), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _exporter.ExportAsync(null!, new MemoryStream()));
    }

    [Fact]
    public async Task Zip_export_without_zip_options_explains_what_to_do()
    {
        var error = await Assert.ThrowsAsync<ExportException>(
            () => _exporter.ExportAsync(TestData.Request(TestData.SalesTable(1)), new MemoryStream()));

        Assert.Contains("ZipExportOptions", error.Message, StringComparison.Ordinal);
    }

    private async Task<byte[]> ArchiveBytesAsync(
        IReadOnlyList<ZipEntrySource> entries,
        CompressionLevel level = CompressionLevel.Optimal)
    {
        var options = new ZipExportOptions { Entries = entries, CompressionLevel = level };
        using var stream = new MemoryStream();
        await _exporter.ExportAsync(ExportRequest.Empty(options), stream);
        return stream.ToArray();
    }

    private async Task<ZipArchive> OpenAsync(IReadOnlyList<ZipEntrySource> entries)
    {
        var bytes = await ArchiveBytesAsync(entries);
        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    private static async Task<string> ReadAsync(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);

        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
