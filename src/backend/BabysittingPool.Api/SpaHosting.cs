using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace BabysittingPool.Api;

/// <summary>
/// Serves the built SPA from <c>wwwroot</c> on the API's origin (ADR 0018, ADR 0006). Client routes fall back to
/// <c>index.html</c>; <c>/api</c> and <c>/health</c> never do, so an unknown API route stays a 404. Nothing is mapped when
/// there is no <c>index.html</c> (development and tests run the SPA from Vite). The security headers of ADR 0014 come from
/// <see cref="SecurityHeadersMiddleware"/>, which wraps these responses too.
/// </summary>
internal static class SpaHosting
{
    private const string IndexFile = "index.html";

    public static bool HasSpa(this IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo(IndexFile).Exists;

    public static IApplicationBuilder UseSpaStaticFiles(this IApplicationBuilder app) =>
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = SetCacheControl });

    public static IEndpointRouteBuilder MapSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        // Unmatched API and health paths must stay 404s rather than an HTML page. These outrank the fallback, which is
        // the lowest-priority endpoint; matching whole segments leaves /apiary or /healthy to the SPA.
        foreach (var prefix in new[] { "/api", "/health" })
        {
            endpoints.Map(prefix, () => Results.NotFound());
            endpoints.Map(prefix + "/{**rest}", () => Results.NotFound());
        }

        endpoints.MapFallbackToFile(IndexFile, new StaticFileOptions { OnPrepareResponse = SetCacheControl });
        return endpoints;
    }

    // Vite names bundles by content hash under /assets, so they never change; every other file (index.html above all)
    // must be revalidated, or a deploy would leave members on a page pointing at bundles that no longer exist.
    private static void SetCacheControl(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;
        headers[HeaderNames.CacheControl] = context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal)
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }
}
