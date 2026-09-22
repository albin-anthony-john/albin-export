using System.Diagnostics.CodeAnalysis;
using Albin.Export.Abstractions;

namespace Albin.Export.AspNetCore;

/// <summary>
/// Parses the export format from a request value such as <c>?format=xlsx</c>.
/// </summary>
/// <remarks>
/// Accepting a format name from the client is the usual way to expose one report endpoint per report
/// rather than one per format. Parsing is strict: anything unrecognized is rejected rather than
/// guessed, so a request can never select an exporter by accident.
/// </remarks>
public static class ExportFormats
{
    /// <summary>
    /// Attempts to parse a format name or file extension.
    /// </summary>
    /// <param name="value">The value to parse, for example <c>pdf</c>, <c>xlsx</c> or <c>.md</c>. Case insensitive.</param>
    /// <param name="format">The parsed format when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value maps to a known format.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out ExportFormat format)
    {
        format = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('.').ToLowerInvariant();
        switch (normalized)
        {
            case "pdf":
                format = ExportFormat.Pdf;
                return true;
            case "excel" or "xlsx" or "xls" or "spreadsheet":
                format = ExportFormat.Excel;
                return true;
            case "csv":
                format = ExportFormat.Csv;
                return true;
            case "html" or "htm":
                format = ExportFormat.Html;
                return true;
            case "word" or "docx" or "doc":
                format = ExportFormat.Word;
                return true;
            case "json":
                format = ExportFormat.Json;
                return true;
            case "xml":
                format = ExportFormat.Xml;
                return true;
            case "markdown" or "md":
                format = ExportFormat.Markdown;
                return true;
            case "zip":
                format = ExportFormat.Zip;
                return true;
            case "png":
                format = ExportFormat.Png;
                return true;
            case "svg":
                format = ExportFormat.Svg;
                return true;
            case "echarts" or "chart":
                format = ExportFormat.ECharts;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Parses a format name, throwing when it is unknown or unsupported by the service.
    /// </summary>
    /// <param name="exportService">The service used to check that an exporter is registered.</param>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exportService"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExportFormatNotSupportedException">The value is unknown or has no registered exporter.</exception>
    public static ExportFormat Parse(this IExportService exportService, string? value)
    {
        ArgumentNullException.ThrowIfNull(exportService);

        if (!TryParse(value, out var format) || !exportService.Supports(format))
        {
            throw new ExportFormatNotSupportedException(format, exportService.SupportedFormats);
        }

        return format;
    }
}
