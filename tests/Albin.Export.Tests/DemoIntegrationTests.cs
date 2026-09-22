using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Albin.Export.Tests;

public sealed class DemoIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DemoIntegrationTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_lists_buffered_and_streamed_endpoints()
    {
        using var response = await _client.GetAsync("/api");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/files/pdf", json, StringComparison.Ordinal);
        Assert.Contains("/api/stream/csv", json, StringComparison.Ordinal);
        Assert.Contains("/api/bundle", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/files/csv", "text/csv")]
    [InlineData("/api/files/json", "application/json")]
    [InlineData("/api/files/xml", "application/xml")]
    [InlineData("/api/files/md", "text/markdown")]
    [InlineData("/api/files/html", "text/html")]
    [InlineData("/api/rows/csv", "text/csv")]
    public async Task Buffered_text_exports_return_the_expected_content_type(string path, string mediaType)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith(mediaType, response.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("North", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(response.Content.Headers.ContentDisposition?.FileNameStar)
            && string.IsNullOrEmpty(response.Content.Headers.ContentDisposition?.FileName));
    }

    [Theory]
    [InlineData("/api/stream/csv", "text/csv")]
    [InlineData("/api/stream/json", "application/json")]
    [InlineData("/api/rows/csv/stream", "text/csv")]
    public async Task Streamed_text_exports_write_the_body_and_content_disposition(string path, string mediaType)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith(mediaType, response.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("North", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition?.ToString()));
    }

    [Theory]
    [InlineData("/api/files/pdf", "%PDF-")]
    [InlineData("/api/files/xlsx", "PK")]
    [InlineData("/api/files/docx", "PK")]
    [InlineData("/api/bundle", "PK")]
    [InlineData("/api/stream/pdf", "%PDF-")]
    public async Task Binary_exports_start_with_the_format_signature(string path, string signature)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var head = System.Text.Encoding.ASCII.GetString(bytes[..signature.Length]);
        Assert.Equal(signature, head);
    }

    [Fact]
    public async Task Unknown_format_is_rejected()
    {
        using var response = await _client.GetAsync("/api/files/exe");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Streamed_csv_uses_an_attachment_file_name()
    {
        using var response = await _client.GetAsync("/api/stream/csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "sales.csv",
            response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty,
            StringComparison.Ordinal);
    }
}
