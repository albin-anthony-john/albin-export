namespace Albin.Export.Excel;

/// <summary>
/// Marks a value as an Excel formula so <see cref="ExcelExporter"/> writes it as a formula instead of text.
/// </summary>
/// <remarks>
/// Formulas are opt-in by design. Plain strings are always written as text, even when they start with
/// <c>=</c>, which means exported user content can never be turned into a live formula. Use this type
/// in a table's <see cref="Documents.ExportTable.TotalsRow"/> when you do want a calculation.
/// </remarks>
/// <example>
/// <code>
/// var table = ExportTable.Create(rows, "Sales")
///     .Column("Region", x =&gt; x.Region)
///     .Column("Amount", x =&gt; x.Amount, format: "N2")
///     .WithTotals("Total", new ExcelFormula("SUM(B2:B{lastRow})"))
///     .Build();
/// </code>
/// </example>
public sealed class ExcelFormula
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelFormula"/> class.
    /// </summary>
    /// <param name="a1Expression">
    /// The formula in A1 notation, without the leading <c>=</c>. The placeholder
    /// <c>{lastRow}</c> is replaced with the last data row of the worksheet, and <c>{column}</c> with
    /// the current column letter.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="a1Expression"/> is <see langword="null"/> or whitespace.</exception>
    public ExcelFormula(string a1Expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(a1Expression);
        Expression = a1Expression.TrimStart('=');
    }

    /// <summary>Gets the formula expression without the leading <c>=</c>.</summary>
    public string Expression { get; }

    /// <inheritdoc />
    public override string ToString() => "=" + Expression;
}
