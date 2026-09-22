using Albin.Export.Abstractions;
using Albin.Export.Chart;
using Albin.Export.Csv;
using Albin.Export.Documents;
using Albin.Export.Json;
using Albin.Export.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace Albin.Export.Tests;

public sealed class ExportServiceTests
{
    [Fact]
    public async Task ExportAsync_returns_a_ready_to_serve_result()
    {
        var service = TestData.Service();

        using var result = await service.ExportAsync(
            TestData.Sales(3),
            ExportFormat.Csv,
            new CsvExportOptions { FileName = "sales" });

        Assert.Equal("sales.csv", result.FileName);
        Assert.Equal(ExportContentTypes.Csv, result.ContentType);
        Assert.Equal(ExportFormat.Csv, result.Format);
        Assert.Equal(0, result.Stream.Position);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task ExportAsync_derives_the_file_name_from_the_document_title()
    {
        var service = TestData.Service();
        using var result = await service.ExportAsync(TestData.SalesDocument(2), ExportFormat.Markdown);
        Assert.Equal("Quarterly sales.md", result.FileName);
    }

    [Fact]
    public void GetFileName_matches_what_an_export_would_produce()
    {
        var service = TestData.Service();
        var request = TestData.Request(TestData.SalesTable(1));
        Assert.Equal("Sales.json", service.GetFileName(request, ExportFormat.Json));
    }

    [Fact]
    public void Unsupported_format_throws_with_an_actionable_message()
    {
        var service = new ExportService([new CsvExporter()]);

        Assert.False(service.Supports(ExportFormat.Pdf));
        var exception = Assert.Throws<ExportFormatNotSupportedException>(
            () => service.GetExporter(ExportFormat.Pdf));

        Assert.Equal(ExportFormat.Pdf, exception.Format);
        Assert.Contains("Albin.Export.Pdf", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Csv", exception.Message, StringComparison.Ordinal);
        Assert.IsType<ExportException>(exception, exactMatch: false);
    }

    [Fact]
    public void SupportedFormats_lists_every_registered_exporter()
    {
        var service = TestData.Service();
        Assert.Contains(ExportFormat.Pdf, service.SupportedFormats);
        Assert.Contains(ExportFormat.Excel, service.SupportedFormats);
        Assert.Contains(ExportFormat.ECharts, service.SupportedFormats);
        Assert.Equal(12, service.SupportedFormats.Count);
    }

    [Fact]
    public void The_last_registration_for_a_format_wins()
    {
        var replacement = new StubExporter(ExportFormat.Csv);
        var service = new ExportService([new CsvExporter(), replacement]);
        Assert.Same(replacement, service.GetExporter(ExportFormat.Csv));
    }

    [Fact]
    public async Task ExportToAsync_writes_straight_to_the_destination()
    {
        var service = TestData.Service();
        using var destination = new MemoryStream();

        await service.ExportToAsync(
            TestData.Request(TestData.SalesTable(4)),
            ExportFormat.Csv,
            destination);

        Assert.True(destination.Length > 0);
    }

    [Fact]
    public async Task ExportToAsync_works_with_a_non_seekable_destination()
    {
        var service = TestData.Service();
        using var inner = new MemoryStream();
        await using var destination = new WriteOnlyStream(inner);

        await service.ExportToAsync(
            TestData.Request(TestData.SalesTable(4)),
            ExportFormat.Excel,
            destination);

        Assert.True(inner.Length > 0);
    }

    [Fact]
    public async Task Disposing_the_result_disposes_the_stream()
    {
        var service = TestData.Service();
        var result = await service.ExportAsync(TestData.Sales(2), ExportFormat.Csv);
        var stream = result.Stream;

        result.Dispose();

        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => result.Stream);
        Assert.Null(result.Length);
        result.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_disposes_the_stream()
    {
        var service = TestData.Service();
        var result = await service.ExportAsync(TestData.Sales(2), ExportFormat.Json);
        var stream = result.Stream;

        await result.DisposeAsync();

        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task ToArrayAsync_returns_the_whole_content()
    {
        var service = TestData.Service();
        using var result = await service.ExportAsync(TestData.Sales(3), ExportFormat.Csv);

        var bytes = await result.ToArrayAsync();

        Assert.Equal(result.Length, bytes.Length);
    }

    [Fact]
    public async Task CopyToAsync_writes_the_content_elsewhere()
    {
        var service = TestData.Service();
        using var result = await service.ExportAsync(TestData.Sales(3), ExportFormat.Csv);
        using var target = new MemoryStream();

        await result.CopyToAsync(target);

        Assert.Equal(result.Length, target.Length);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        var service = TestData.Service();

        Assert.Throws<ArgumentNullException>(() => new ExportService(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExportAsync((ExportRequest)null!, ExportFormat.Csv));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExportAsync((IEnumerable<int>)null!, ExportFormat.Csv));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExportAsync((ExportDocument)null!, ExportFormat.Csv));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExportToAsync(TestData.Request(TestData.SalesTable(1)), ExportFormat.Csv, null!));
    }

    [Fact]
    public async Task Failed_exports_propagate_and_release_the_output_buffer()
    {
        var service = new ExportService([new ThrowingExporter()]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExportAsync(TestData.Sales(1), ExportFormat.Csv));
    }

    [Fact]
    public void AddAlbinExport_registers_the_service_and_the_selected_exporters()
    {
        var services = new ServiceCollection();
        services.AddAlbinExport(export => export.AddCsv().AddJson().AddPdf().AddCharts());

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IExportService>();

        Assert.True(service.Supports(ExportFormat.Csv));
        Assert.True(service.Supports(ExportFormat.Json));
        Assert.True(service.Supports(ExportFormat.Pdf));
        Assert.True(service.Supports(ExportFormat.Svg));
        Assert.False(service.Supports(ExportFormat.Excel));
        Assert.Same(service, provider.GetRequiredService<IExportService>());
    }

    [Fact]
    public void Registering_the_same_exporter_twice_is_a_no_op()
    {
        var services = new ServiceCollection();
        services.AddAlbinExport(export => export.AddCsv().AddCsv().AddCsv());

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetRequiredService<IEnumerable<IExporter>>());
    }

    [Fact]
    public void An_application_can_replace_a_built_in_exporter()
    {
        var services = new ServiceCollection();
        services.AddAlbinExport(export => export
            .AddCsv()
            .AddExporter(new StubExporter(ExportFormat.Csv)));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<StubExporter>(provider.GetRequiredService<IExportService>().GetExporter(ExportFormat.Csv));
    }

    [Fact]
    public void ExportServiceBuilder_rejects_nulls()
    {
        var services = new ServiceCollection();
        services.AddAlbinExport(export =>
        {
            Assert.Throws<ArgumentNullException>(() => export.AddExporter((IExporter)null!));
            Assert.Throws<ArgumentNullException>(() => export.AddExporter((Func<IServiceProvider, StubExporter>)null!));
        });

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddAlbinExport());
    }

    private sealed class StubExporter(ExportFormat format) : IExporter
    {
        public ExportFormat Format { get; } = format;

        public string ContentType => ExportContentTypes.Binary;

        public string FileExtension => ".bin";

        public Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingExporter : IExporter
    {
        public ExportFormat Format => ExportFormat.Csv;

        public string ContentType => ExportContentTypes.Csv;

        public string FileExtension => ".csv";

        public Task ExportAsync(ExportRequest request, Stream destination, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}

/// <summary>A write-only, non-seekable stream, standing in for an HTTP response body.</summary>
internal sealed class WriteOnlyStream(Stream inner) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => inner.WriteAsync(buffer, cancellationToken);
}
