namespace Albin.Export.Abstractions;

/// <summary>
/// Identifies an export output format.
/// </summary>
/// <remarks>
/// A format is only usable at runtime when the matching exporter package is installed and registered,
/// for example <c>Albin.Export.Pdf</c> for <see cref="Pdf"/>. Use
/// <see cref="IExportService.Supports(ExportFormat)"/> to probe availability.
/// </remarks>
public enum ExportFormat
{
    /// <summary>Portable Document Format (<c>.pdf</c>). Provided by <c>Albin.Export.Pdf</c>.</summary>
    Pdf = 1,

    /// <summary>Office Open XML workbook (<c>.xlsx</c>). Provided by <c>Albin.Export.Excel</c>.</summary>
    Excel = 2,

    /// <summary>Comma separated values (<c>.csv</c>). Provided by <c>Albin.Export.Csv</c>.</summary>
    Csv = 3,

    /// <summary>Standalone HTML document (<c>.html</c>). Provided by <c>Albin.Export.Html</c>.</summary>
    Html = 4,

    /// <summary>Office Open XML document (<c>.docx</c>). Provided by <c>Albin.Export.Word</c>.</summary>
    Word = 5,

    /// <summary>UTF-8 JSON (<c>.json</c>). Provided by <c>Albin.Export.Json</c>.</summary>
    Json = 6,

    /// <summary>UTF-8 XML (<c>.xml</c>). Provided by <c>Albin.Export.Xml</c>.</summary>
    Xml = 7,

    /// <summary>CommonMark-style Markdown (<c>.md</c>). Provided by <c>Albin.Export.Markdown</c>.</summary>
    Markdown = 8,

    /// <summary>ZIP archive (<c>.zip</c>). Provided by <c>Albin.Export.Zip</c>.</summary>
    Zip = 9,

    /// <summary>Raster chart image (<c>.png</c>). Provided by <c>Albin.Export.Chart</c>.</summary>
    Png = 10,

    /// <summary>Vector chart image (<c>.svg</c>). Provided by <c>Albin.Export.Chart</c>.</summary>
    Svg = 11,

    /// <summary>Apache ECharts option object serialized as JSON. Provided by <c>Albin.Export.Chart</c>.</summary>
    ECharts = 12,
}
