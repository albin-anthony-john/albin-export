using System.Globalization;
using Albin.Export.Abstractions;
using Albin.Export.Documents;

namespace Albin.Export.Chart;

/// <summary>
/// Works out which chart a request wants to render.
/// </summary>
internal static class ChartSpecResolver
{
    public static async ValueTask<ChartSpec> ResolveAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        if (request.Options is ChartExportOptions { Chart: not null } explicitOptions)
        {
            return explicitOptions.Chart;
        }

        foreach (var section in request.Document.Sections)
        {
            if (section is ExportChart chart)
            {
                return chart.Chart;
            }
        }

        var table = request.PrimaryTable;
        if (table is null || table.Columns.Count < 2)
        {
            throw new ExportException(
                "The request contains no chart. Add an ExportChart section, set ChartExportOptions.Chart, "
                + "or supply a table whose first column holds the categories and whose remaining columns are numeric.");
        }

        return await FromTableAsync(table, request, cancellationToken);
    }

    private static async ValueTask<ChartSpec> FromTableAsync(
        ExportTable table,
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var maxCategories = request.Options is ChartExportOptions options ? Math.Max(options.MaxDerivedCategories, 1) : 200;
        var candidates = new List<int>();
        for (var i = 1; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            if (column.IsNumeric || column.ValueType == typeof(object))
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            throw new ExportException(
                $"Table '{table.Name ?? "(unnamed)"}' has no numeric column to chart. "
                + "Declare numeric columns or provide an explicit ChartSpec.");
        }

        var categories = new List<string>();
        var values = candidates.ToDictionary(index => index, _ => new List<double?>());

        await foreach (var row in table.Rows.ReadAsync(cancellationToken))
        {
            if (categories.Count >= maxCategories)
            {
                break;
            }

            categories.Add(ExportValueFormatter.Format(row[0], table.Columns[0], request.Options));
            foreach (var index in candidates)
            {
                values[index].Add(index < row.FieldCount ? ToDouble(row[index]) : null);
            }
        }

        var series = candidates
            .Where(index => values[index].Any(static v => v.HasValue))
            .Select(index => new ChartSeries(table.Columns[index].Header, values[index]))
            .ToArray();

        if (series.Length == 0)
        {
            throw new ExportException(
                $"Table '{table.Name ?? "(unnamed)"}' contains no numeric values to chart.");
        }

        return new ChartSpec(ChartType.Bar, categories, series)
        {
            Title = request.Document.Title ?? table.Name,
            Subtitle = request.Document.Subtitle,
        };
    }

    private static double? ToDouble(object? value) => value switch
    {
        null or DBNull => null,
        double d => double.IsFinite(d) ? d : null,
        float f => float.IsFinite(f) ? f : null,
        decimal m => (double)m,
        byte or sbyte or short or ushort or int or uint or long or ulong =>
            Convert.ToDouble(value, CultureInfo.InvariantCulture),
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };
}
