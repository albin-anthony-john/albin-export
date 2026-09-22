namespace Albin.Export.Html;

/// <summary>
/// The built-in stylesheet used by <see cref="HtmlExporter"/>.
/// </summary>
/// <remarks>
/// Exposed so applications can reuse the exact same look in a Razor layout, or start from it when
/// supplying <see cref="HtmlExportOptions.AdditionalCss"/>.
/// </remarks>
public static class HtmlStyles
{
    /// <summary>
    /// Gets the default stylesheet: a neutral, responsive, print-friendly report style with no
    /// external fonts or network requests.
    /// </summary>
    public const string Default = """
        :root {
          --fg: #24292f;
          --muted: #57606a;
          --border: #d0d7de;
          --accent: #0969da;
          --stripe: #f6f8fa;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          padding: 24px;
          color: var(--fg);
          background: #fff;
          font: 15px/1.55 -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
          -webkit-text-size-adjust: 100%;
        }
        .export { max-width: 1100px; margin: 0 auto; }
        .export-title { margin: 0 0 4px; font-size: 26px; line-height: 1.25; }
        .export-subtitle { margin: 0 0 24px; color: var(--muted); }
        .export h2 { margin: 28px 0 10px; font-size: 20px; }
        .export h3 { margin: 22px 0 8px; font-size: 17px; }
        .export h4, .export h5, .export h6 { margin: 18px 0 8px; }
        .export p { margin: 0 0 12px; }
        .export p.muted { color: var(--muted); }
        .export p.bold { font-weight: 600; }
        .table-wrap { overflow-x: auto; margin: 0 0 20px; }
        table { width: 100%; border-collapse: collapse; font-size: 14px; }
        caption { caption-side: top; text-align: left; font-weight: 600; padding: 0 0 8px; }
        th, td { padding: 7px 10px; border-bottom: 1px solid var(--border); vertical-align: top; }
        th { background: var(--stripe); text-align: left; font-weight: 600; white-space: nowrap; }
        tbody tr.stripe { background: var(--stripe); }
        tfoot td { font-weight: 600; border-top: 2px solid var(--border); }
        td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
        td.center, th.center { text-align: center; }
        .empty { color: var(--muted); font-style: italic; padding: 12px 0; }
        figure { margin: 0 0 24px; }
        figure svg, figure img { max-width: 100%; height: auto; }
        .echarts { width: 100%; min-height: 320px; }
        .export-footer { margin-top: 32px; padding-top: 12px; border-top: 1px solid var(--border); color: var(--muted); font-size: 13px; }
        @media (max-width: 640px) {
          body { padding: 14px; }
          th, td { padding: 6px 7px; }
        }
        @media print {
          body { padding: 0; font-size: 11pt; }
          .table-wrap { overflow: visible; }
          table { page-break-inside: auto; }
          tr { page-break-inside: avoid; page-break-after: auto; }
          thead { display: table-header-group; }
          tfoot { display: table-footer-group; }
          figure, h2, h3 { page-break-inside: avoid; }
          .page-break { page-break-after: always; }
          a[href]::after { content: ""; }
        }
        """;
}
