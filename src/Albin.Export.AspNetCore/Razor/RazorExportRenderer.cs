using System.Text;
using Albin.Export.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Albin.Export.AspNetCore.Razor;

/// <summary>
/// The default <see cref="IRazorExportRenderer"/>, built on the MVC Razor view engine.
/// </summary>
/// <remarks>
/// Rendering works both inside a request (reusing the current <see cref="HttpContext"/>) and outside
/// one, for example from a background job, by creating a minimal context backed by the application's
/// service provider.
/// </remarks>
public sealed class RazorExportRenderer : IRazorExportRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RazorExportRenderer"/> class.
    /// </summary>
    /// <param name="viewEngine">The Razor view engine.</param>
    /// <param name="tempDataProvider">The temp data provider required by the view context.</param>
    /// <param name="services">The application service provider.</param>
    /// <param name="httpContextAccessor">Optional accessor used to reuse the ambient HTTP context.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public RazorExportRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider services,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(viewEngine);
        ArgumentNullException.ThrowIfNull(tempDataProvider);
        ArgumentNullException.ThrowIfNull(services);

        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _services = services;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(
        string viewName,
        object? model,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        cancellationToken.ThrowIfCancellationRequested();

        var httpContext = _httpContextAccessor?.HttpContext
            ?? new DefaultHttpContext { RequestServices = _services };
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());
        var view = FindView(actionContext, viewName);

        var viewData = new ViewDataDictionary<object?>(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model,
        };

        var writer = new StringWriter(new StringBuilder(8 * 1024));
        await using (writer)
        {
            var viewContext = new ViewContext(
                actionContext,
                view,
                viewData,
                new TempDataDictionary(httpContext, _tempDataProvider),
                writer,
                new HtmlHelperOptions());

            await view.RenderAsync(viewContext);
            await writer.FlushAsync(cancellationToken);
            return writer.ToString();
        }
    }

    /// <inheritdoc />
    public async Task<ExportResult> RenderToResultAsync(
        string viewName,
        object? model,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var html = await RenderAsync(viewName, model, cancellationToken);

        var buffer = ExportStreams.CreateBuffer(Encoding.UTF8.GetMaxByteCount(html.Length));
        try
        {
            await ExportStreams.WriteTextAsync(buffer, html, byteOrderMark: false, cancellationToken)
                ;
            buffer.Position = 0;

            return new ExportResult(
                ExportFileName.Sanitize(fileName ?? viewName, ".html"),
                ExportContentTypes.Html,
                buffer,
                ExportFormat.Html);
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    private IView FindView(ActionContext actionContext, string viewName)
    {
        foreach (var candidate in ViewPathCandidates(viewName))
        {
            var byPath = _viewEngine.GetView(executingFilePath: null, viewPath: candidate, isMainPage: true);
            if (byPath.Success)
            {
                return byPath.View;
            }
        }

        var byName = _viewEngine.FindView(actionContext, viewName, isMainPage: true);
        if (byName.Success)
        {
            return byName.View;
        }

        byName = _viewEngine.FindView(actionContext, "Reports/" + viewName, isMainPage: true);
        if (byName.Success)
        {
            return byName.View;
        }

        var searched = byName.SearchedLocations;
        throw new ExportException(
            $"Razor view '{viewName}' was not found. Searched: {string.Join(", ", searched)}.");
    }

    private static IEnumerable<string> ViewPathCandidates(string viewName)
    {
        yield return viewName;
        if (viewName.StartsWith('~') || viewName.StartsWith('/'))
        {
            yield break;
        }

        var relative = viewName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? viewName
            : viewName + ".cshtml";
        yield return "~/Views/" + relative;
        yield return "/Views/" + relative;
    }
}
