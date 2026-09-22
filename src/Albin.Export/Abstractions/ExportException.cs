namespace Albin.Export.Abstractions;

/// <summary>
/// Base class for errors raised by the export framework.
/// </summary>
public class ExportException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ExportException"/> class.</summary>
    public ExportException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ExportException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ExportException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ExportException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public ExportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an export is requested for a format that has no registered exporter, usually because
/// the matching package has not been installed or registered.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ExportFormatNotSupportedException"/> class.
/// </remarks>
/// <param name="format">The unsupported format.</param>
/// <param name="registered">The formats that are registered.</param>
public sealed class ExportFormatNotSupportedException(ExportFormat format, IEnumerable<ExportFormat>? registered = null) : ExportException(BuildMessage(format, registered))
{
    /// <summary>Gets the format that could not be exported.</summary>
    public ExportFormat Format { get; } = format;

    private static string BuildMessage(ExportFormat format, IEnumerable<ExportFormat>? registered)
    {
        var available = registered is null ? string.Empty : string.Join(", ", registered.Order());
        var hint = SuggestPackage(format);
        return $"No exporter is registered for '{format}'. Install and register {hint}. "
            + $"Registered formats: {(available.Length == 0 ? "(none)" : available)}.";
    }

    private static string SuggestPackage(ExportFormat format) => format switch
    {
        ExportFormat.Pdf => "Albin.Export.Pdf (export.AddPdf())",
        ExportFormat.Excel => "Albin.Export.Excel (export.AddExcel())",
        ExportFormat.Csv => "Albin.Export.Csv (export.AddCsv())",
        ExportFormat.Html => "Albin.Export.Html (export.AddHtml())",
        ExportFormat.Word => "Albin.Export.Word (export.AddWord())",
        ExportFormat.Json => "Albin.Export.Json (export.AddJson())",
        ExportFormat.Xml => "Albin.Export.Xml (export.AddXml())",
        ExportFormat.Markdown => "Albin.Export.Markdown (export.AddMarkdown())",
        ExportFormat.Zip => "Albin.Export.Zip (export.AddZip())",
        ExportFormat.Png or ExportFormat.Svg or ExportFormat.ECharts => "Albin.Export.Chart (export.AddCharts())",
        _ => "the exporter that provides this format",
    };
}
