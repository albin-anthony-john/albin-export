namespace Albin.Export.Documents;

/// <summary>
/// Customizes how a property is projected when columns are derived automatically by
/// <see cref="ExportTable.FromItems{T}(IEnumerable{T}, string?)"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportColumnAttribute : Attribute
{
    /// <summary>Gets or sets the header text. Defaults to the property name.</summary>
    public string? Header { get; set; }

    /// <summary>Gets or sets the .NET format string applied to the value.</summary>
    public string? Format { get; set; }

    /// <summary>Gets or sets the sort order. Columns without an order keep declaration order after ordered ones.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the horizontal alignment.</summary>
    public ExportAlignment Alignment { get; set; } = ExportAlignment.Auto;

    /// <summary>Gets or sets the width hint in characters. A value of zero means "not set".</summary>
    public double Width { get; set; }
}

/// <summary>
/// Excludes a property from automatically derived columns.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportIgnoreAttribute : Attribute
{
}
