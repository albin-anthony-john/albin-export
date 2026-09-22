using Albin.Export;
using Albin.Export.Chart;
using Albin.Export.Csv;
using Albin.Export.Demo;
using Albin.Export.Excel;
using Albin.Export.Html;
using Albin.Export.Json;
using Albin.Export.Markdown;
using Albin.Export.Pdf;
using Albin.Export.Word;
using Albin.Export.Xml;
using Albin.Export.Zip;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAlbinExport(export => export
    .AddPdf()
    .AddExcel()
    .AddCsv()
    .AddHtml()
    .AddWord()
    .AddJson()
    .AddXml()
    .AddMarkdown()
    .AddZip()
    .AddCharts());

builder.Services.AddSingleton<SalesReportService>();

var app = builder.Build();

app.MapControllers();
app.Run();

