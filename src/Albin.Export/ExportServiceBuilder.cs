using Albin.Export.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Albin.Export;

/// <summary>
/// Collects the exporters that should be available to <see cref="IExportService"/>.
/// </summary>
/// <remarks>
/// Each format package adds an extension method on this type, for example <c>AddPdf()</c> in
/// <c>Albin.Export.Pdf</c>, so an application only registers the formats it actually uses.
/// </remarks>
public sealed class ExportServiceBuilder
{
    internal ExportServiceBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>Gets the underlying service collection, for advanced scenarios.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers an exporter type as a singleton. Registering the same type twice is a no-op.
    /// </summary>
    /// <typeparam name="TExporter">The exporter implementation type.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    public ExportServiceBuilder AddExporter<TExporter>()
        where TExporter : class, IExporter
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExporter, TExporter>());
        return this;
    }

    /// <summary>
    /// Registers an exporter instance.
    /// </summary>
    /// <param name="exporter">The exporter instance. It must be thread safe.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exporter"/> is <see langword="null"/>.</exception>
    public ExportServiceBuilder AddExporter(IExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        Services.AddSingleton(exporter);
        return this;
    }

    /// <summary>
    /// Registers an exporter created from the service provider.
    /// </summary>
    /// <typeparam name="TExporter">The exporter implementation type.</typeparam>
    /// <param name="factory">The factory invoked once to create the singleton.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public ExportServiceBuilder AddExporter<TExporter>(Func<IServiceProvider, TExporter> factory)
        where TExporter : class, IExporter
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExporter, TExporter>(factory));
        return this;
    }
}
