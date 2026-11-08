using Microsoft.AspNetCore.Http.Features;

namespace BabysittingPool.Api;

/// <summary>
/// Caps request bodies at <see cref="MaxBodyBytes"/> (ADR 0014). Kestrel's default is about 30 MB, and the anonymous
/// endpoints read the whole body into memory before they look at the token, which a 256 MB Machine cannot afford
/// a few times at once. A declared <c>Content-Length</c> over the cap is refused up front; the server's own limit
/// is lowered too, so a chunked body is cut off as well.
/// </summary>
internal static class RequestSizeLimitMiddleware
{
    public const long MaxBodyBytes = 256 * 1024;

    public static IApplicationBuilder UseRequestSizeLimit(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > MaxBodyBytes)
            {
                await Problems.PayloadTooLarge().ExecuteAsync(context);
                return;
            }

            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = MaxBodyBytes;
            }

            await next(context);
        });
}
