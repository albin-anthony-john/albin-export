namespace Albin.Export;

/// <summary>
/// The media types and file extensions produced by the built-in exporters.
/// </summary>
public static class ExportContentTypes
{
    /// <summary><c>application/pdf</c></summary>
    public const string Pdf = "application/pdf";

    /// <summary><c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c></summary>
    public const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary><c>text/csv; charset=utf-8</c></summary>
    public const string Csv = "text/csv; charset=utf-8";

    /// <summary><c>text/html; charset=utf-8</c></summary>
    public const string Html = "text/html; charset=utf-8";

    /// <summary><c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c></summary>
    public const string Word = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary><c>application/json; charset=utf-8</c></summary>
    public const string Json = "application/json; charset=utf-8";

    /// <summary><c>application/xml; charset=utf-8</c></summary>
    public const string Xml = "application/xml; charset=utf-8";

    /// <summary><c>text/markdown; charset=utf-8</c></summary>
    public const string Markdown = "text/markdown; charset=utf-8";

    /// <summary><c>application/zip</c></summary>
    public const string Zip = "application/zip";

    /// <summary><c>image/png</c></summary>
    public const string Png = "image/png";

    /// <summary><c>image/svg+xml; charset=utf-8</c></summary>
    public const string Svg = "image/svg+xml; charset=utf-8";

    /// <summary><c>application/octet-stream</c></summary>
    public const string Binary = "application/octet-stream";
}
