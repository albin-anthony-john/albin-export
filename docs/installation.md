# Installation

Albin.Export targets **.NET 10** (current LTS). Consume it as project references from this repository, or pack and install the NuGet packages.

## From this repository

```bash
dotnet pack -c Release
```

Packages land in `artifacts/` if you set `PackageOutputPath`, otherwise under each project's `bin/Release`.

```bash
dotnet add package Albin.Export --source /path/to/nupkgs
```

## Package-by-package

The core package is required. Everything else is optional.

```xml
<ItemGroup>
  <PackageReference Include="Albin.Export" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Pdf" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Excel" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Csv" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Html" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Word" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Json" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Xml" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Markdown" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Zip" Version="1.0.0" />
  <PackageReference Include="Albin.Export.Chart" Version="1.0.0" />
  <PackageReference Include="Albin.Export.AspNetCore" Version="1.0.0" />
</ItemGroup>
```

`Albin.Export.Html`, `Albin.Export.Pdf` and `Albin.Export.Word` reference `Albin.Export.Chart` so documents can embed server-rendered charts without a second package reference.

## Runtime requirements

| Format | Extra runtime needs |
| --- | --- |
| PDF | At least one TrueType/OpenType font on the host. Windows already has Arial. Linux/Docker: install `fonts-dejavu-core` (see the demo Dockerfile). |
| Excel / Word | None (pure managed). |
| CSV / JSON / XML / Markdown / HTML / ZIP / SVG / PNG / ECharts | None. |
| Razor HTML | The host must call `AddControllersWithViews()` or `AddRazorPages()`. |

No native image library and no browser (Chrome/Playwright) are required. PNG charts are rasterized in managed code.

## Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
```

## License of dependencies

| Package | License |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions | MIT |
| PDFsharp-MigraDoc | MIT |
| ClosedXML | MIT |
| CsvHelper | MS-PL or Apache-2.0 |
| DocumentFormat.OpenXml | MIT |
