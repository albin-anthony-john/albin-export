using Albin.Export.Abstractions;

namespace Albin.Export.Tests;

public sealed class ExportFileNameTests
{
    [Theory]
    [InlineData("report", "report")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config", "config")]
    [InlineData("C:\\temp\\evil.txt", "evil.txt")]
    [InlineData("/absolute/path/file", "file")]
    [InlineData("\\\\server\\share\\file", "file")]
    [InlineData("na:me", "me")]
    [InlineData("a<b>c|d?e*f\"g", "abcdefg")]
    [InlineData("", "export")]
    [InlineData("   ", "export")]
    [InlineData("...", "export")]
    [InlineData("..", "export")]
    [InlineData("trailing...", "trailing")]
    [InlineData(".hidden", "hidden")]
    public void Sanitize_removes_path_and_invalid_characters(string input, string expected)
        => Assert.Equal(expected, ExportFileName.Sanitize(input));

    [Fact]
    public void Sanitize_handles_null() => Assert.Equal("export", ExportFileName.Sanitize(null));

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public void Sanitize_escapes_reserved_windows_device_names(string input)
        => Assert.Equal("_" + input, ExportFileName.Sanitize(input));

    [Fact]
    public void Sanitize_appends_extension_once()
    {
        Assert.Equal("report.pdf", ExportFileName.Sanitize("report", ".pdf"));
        Assert.Equal("report.pdf", ExportFileName.Sanitize("report.pdf", ".pdf"));
        Assert.Equal("report.pdf", ExportFileName.Sanitize("report.PDF", "pdf"));
        Assert.Equal("report.csv.pdf", ExportFileName.Sanitize("report.csv", ".pdf"));
    }

    [Fact]
    public void Sanitize_truncates_very_long_names()
    {
        var name = ExportFileName.Sanitize(new string('a', 500), ".csv");
        Assert.True(name.Length <= 124, $"Unexpected length {name.Length}.");
        Assert.EndsWith(".csv", name, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_keeps_unicode_characters()
        => Assert.Equal("отчёт-2026-日本", ExportFileName.Sanitize("отчёт-2026-日本"));

    [Theory]
    [InlineData("a.txt", true)]
    [InlineData("folder/a.txt", true)]
    [InlineData("deep/nested/folder/a.txt", true)]
    [InlineData("../a.txt", false)]
    [InlineData("folder/../../a.txt", false)]
    [InlineData("/a.txt", false)]
    [InlineData("C:/a.txt", false)]
    [InlineData("folder\\a.txt", false)]
    [InlineData("./a.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSafeEntryName_rejects_traversal(string? input, bool expected)
        => Assert.Equal(expected, ExportFileName.IsSafeEntryName(input));

    [Theory]
    [InlineData("../../etc/passwd", "etc/passwd")]
    [InlineData("/var/log/app.log", "var/log/app.log")]
    [InlineData("a\\b\\c.txt", "a/b/c.txt")]
    [InlineData("folder/../secret.txt", "folder/secret.txt")]
    [InlineData("", "export")]
    public void SanitizeEntryName_produces_relative_safe_names(string input, string expected)
    {
        var sanitized = ExportFileName.SanitizeEntryName(input);
        Assert.Equal(expected, sanitized);
        Assert.True(ExportFileName.IsSafeEntryName(sanitized));
    }

    [Fact]
    public void ExportResult_sanitizes_the_file_name_it_is_given()
    {
        using var result = new ExportResult(
            "../../../etc/passwd.csv",
            ExportContentTypes.Csv,
            new MemoryStream(),
            ExportFormat.Csv);

        Assert.Equal("passwd.csv", result.FileName);
    }
}
