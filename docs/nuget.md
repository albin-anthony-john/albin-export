# NuGet packaging

## Metadata

Shared by every `src/` project (`Directory.Build.props` + `src/Directory.Build.props`):

| Property | Value |
| --- | --- |
| `PackageId` | per project (`Albin.Export`, `Albin.Export.Pdf`, ...) |
| `VersionPrefix` | `1.0.0` |
| `Authors` | Albin |
| `License` | MIT (`PackageLicenseExpression`) |
| `RepositoryUrl` / `PackageProjectUrl` | set in `Directory.Build.props` |
| `PackageTags` | export, pdf, excel, csv, ... |
| `PackageReadmeFile` | repo `README.md` |
| Symbols | `snupkg` |
| XML documentation | enabled |
| Deterministic | `true`; SourceLink via `PublishRepositoryUrl` and `EmbedUntrackedSources` |
| Treat warnings as errors | `true` |
| Nullable | enabled |

CI should set `CI=true` (or `ContinuousIntegrationBuild=true`) so deterministic source paths are used.

## Pack

From the repository root:

```bash
dotnet pack -c Release
```

That produces one `.nupkg` and one `.snupkg` per packable project under `src/*/bin/Release`.

To gather them:

```bash
dotnet pack -c Release -o artifacts
```

## Consume locally

```bash
dotnet nuget add source ./artifacts --name albin-export-local
dotnet add package Albin.Export --source albin-export-local
```

## Publishing

```bash
dotnet nuget push artifacts/*.nupkg --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
```

Push `.snupkg` next to the packages so debugger symbols resolve.

Do not pack `samples/`, `tests/`, or `benchmarks/` — they set `IsPackable=false`.
