using Albin.Export.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Albin.Export;

/// <summary>
/// Registration helpers for the Albin.Export framework.
/// </summary>
public static class ExportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IExportService"/> and the exporters selected through the builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Callback used to add exporters, for example
    /// <c>export =&gt; export.AddPdf().AddExcel().AddCsv()</c>.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// builder.Services.AddAlbinExport(export =&gt; export
    ///     .AddPdf()
    ///     .AddExcel()
    ///     .AddCsv()
    ///     .AddJson());
    /// </code>
    /// </example>
    public static IServiceCollection AddAlbinExport(
        this IServiceCollection services,
        Action<ExportServiceBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExportService, ExportService>();
        configure?.Invoke(new ExportServiceBuilder(services));
        return services;
    }
}
