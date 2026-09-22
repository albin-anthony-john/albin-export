using Albin.Export.AspNetCore.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Albin.Export.AspNetCore;

/// <summary>
/// Registration helpers for the ASP.NET Core integration.
/// </summary>
public static class AspNetCoreExportBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="IRazorExportRenderer"/> so Razor views can be rendered into export results.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// The application must already have MVC or Razor Pages configured
    /// (<c>AddControllersWithViews()</c> or <c>AddRazorPages()</c>), because the renderer uses the
    /// MVC view engine.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddRazorRendering(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton<IRazorExportRenderer, RazorExportRenderer>();
        return builder;
    }

    /// <summary>
    /// Registers the Razor renderer and the Razor-backed HTML exporter.
    /// </summary>
    /// <param name="builder">The export builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// Call this after <c>AddHtml()</c> to replace the built-in HTML writer with your own Razor
    /// templates, or instead of it when every HTML export goes through a view.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ExportServiceBuilder AddRazorHtml(this ExportServiceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddRazorRendering().AddExporter<RazorHtmlExporter>();
    }
}
