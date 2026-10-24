namespace BabysittingPool.Api;

/// <summary>
/// Browser hardening on every response (ADR 0014): a CSP with no inline script, <c>nosniff</c>, and no Referrer, because the
/// magic-link token travels in the SPA page's query string. <c>/api</c> responses are also never cached. HSTS is added
/// separately in Production (<c>UseHsts</c>). No CORS policy is registered, so cross-origin calls fail by default (ADR 0006).
/// </summary>
internal static class SecurityHeadersMiddleware
{
    public const string ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // OnStarting, so the headers also reach responses that short-circuit later (CSRF 403, 429, 401).
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            await next(context);
        });
}
