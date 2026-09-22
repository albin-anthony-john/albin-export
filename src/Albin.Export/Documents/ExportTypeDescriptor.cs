using System.Linq.Expressions;
using System.Reflection;

namespace Albin.Export.Documents;

/// <summary>
/// Caches the automatically derived column layout and compiled value accessors for a type.
/// </summary>
/// <typeparam name="T">The item type being described.</typeparam>
/// <remarks>
/// The descriptor is created once per closed generic type. Accessors are compiled expression trees,
/// so per-row cost is close to a direct property read. Types that are not reflected over
/// (<see cref="string"/>, primitives, <see cref="DateTime"/>, ...) produce a single <c>Value</c> column.
/// </remarks>
internal sealed class ExportTypeDescriptor<T>
{
    private ExportTypeDescriptor()
    {
        var type = typeof(T);
        TypeName = FriendlyName(type);

        if (IsScalar(type))
        {
            Columns = [new ExportColumn("Value") { ValueType = type }];
            Accessors = [static item => item];
            return;
        }

        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(static p => p.GetCustomAttribute<ExportIgnoreAttribute>() is null)
            .Select(static (p, index) => (Property: p, Index: index, Meta: p.GetCustomAttribute<ExportColumnAttribute>()))
            .OrderBy(static x => x.Meta?.Order ?? 0)
            .ThenBy(static x => x.Index)
            .ToArray();

        var columns = new ExportColumn[properties.Length];
        var accessors = new Func<T, object?>[properties.Length];
        for (var i = 0; i < properties.Length; i++)
        {
            var (property, _, meta) = properties[i];
            columns[i] = new ExportColumn(meta?.Header ?? property.Name)
            {
                Format = meta?.Format,
                Alignment = meta?.Alignment ?? ExportAlignment.Auto,
                Width = meta is { Width: > 0 } ? meta.Width : null,
                ValueType = property.PropertyType,
            };
            accessors[i] = CompileAccessor(property);
        }

        Columns = columns;
        Accessors = accessors;
    }

    /// <summary>Gets the shared descriptor for <typeparamref name="T"/>.</summary>
    public static ExportTypeDescriptor<T> Instance { get; } = new();

    /// <summary>Gets the derived columns.</summary>
    public IReadOnlyList<ExportColumn> Columns { get; }

    /// <summary>Gets the compiled accessors, aligned with <see cref="Columns"/>.</summary>
    public IReadOnlyList<Func<T, object?>> Accessors { get; }

    /// <summary>Gets a display name for <typeparamref name="T"/>, used as a default table name.</summary>
    public string TypeName { get; }

    private static Func<T, object?> CompileAccessor(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(T), "item");
        Expression instance = property.DeclaringType!.IsAssignableFrom(typeof(T))
            ? parameter
            : Expression.Convert(parameter, property.DeclaringType!);

        Expression body = Expression.Convert(Expression.Property(instance, property), typeof(object));
        if (!typeof(T).IsValueType)
        {
            // Null items must not throw; they render as empty cells.
            body = Expression.Condition(
                Expression.Equal(parameter, Expression.Constant(null, typeof(T))),
                Expression.Constant(null, typeof(object)),
                body);
        }

        return Expression.Lambda<Func<T, object?>>(body, parameter).Compile();
    }

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(Uri)
            || underlying == typeof(object);
    }

    private static string FriendlyName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }
}
