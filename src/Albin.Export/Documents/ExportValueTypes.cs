namespace Albin.Export.Documents;

/// <summary>
/// Helpers for classifying CLR types used by table cells.
/// </summary>
public static class ExportValueTypes
{
    /// <summary>
    /// Determines whether the supplied type is a numeric type, ignoring <see cref="Nullable{T}"/> wrapping.
    /// </summary>
    /// <param name="type">The type to inspect. May be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the type is an integral, floating point or decimal type.</returns>
    public static bool IsNumeric(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum)
        {
            return false;
        }

        return Type.GetTypeCode(underlying) is TypeCode.Byte or TypeCode.SByte
            or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32
            or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    /// <summary>
    /// Determines whether the supplied type represents a date or time, ignoring <see cref="Nullable{T}"/> wrapping.
    /// </summary>
    /// <param name="type">The type to inspect. May be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/> and <see cref="TimeOnly"/>.</returns>
    public static bool IsDateOrTime(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly);
    }
}
