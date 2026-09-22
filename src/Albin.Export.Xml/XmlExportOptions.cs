using Albin.Export.Abstractions;

namespace Albin.Export.Xml;

/// <summary>
/// Options for <see cref="XmlExporter"/>.
/// </summary>
public sealed class XmlExportOptions : ExportOptions
{
    /// <summary>
    /// Gets or sets the root element name. Defaults to <c>export</c>.
    /// </summary>
    public string RootElementName { get; set; } = "export";

    /// <summary>
    /// Gets or sets the element name used for each row. Defaults to <c>row</c>.
    /// </summary>
    public string RowElementName { get; set; } = "row";

    /// <summary>
    /// Gets or sets a value indicating whether the output is indented. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Indent { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the XML declaration is omitted. Defaults to <see langword="false"/>.
    /// </summary>
    public bool OmitXmlDeclaration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// is used to serialize <see cref="ExportRequest.Data"/> instead of writing the document's tables.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// The serializer mode mirrors your DTO shape exactly, including nesting, and honours the
    /// <c>System.Xml.Serialization</c> attributes. It requires the payload type to be
    /// serializer-friendly: a public type with a public parameterless constructor and public settable
    /// members. Records with positional parameters, interfaces and anonymous types are not supported
    /// by <see cref="System.Xml.Serialization.XmlSerializer"/>; leave this off for those.
    /// </remarks>
    public bool UseXmlSerializer { get; set; }
}
