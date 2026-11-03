namespace BabysittingPool.Api;

/// <summary>
/// State-changing <c>/api</c> calls must carry <c>X-Requested-With</c>, which a cross-site form cannot send (ADR 0006).
/// </summary>
internal static class CsrfMiddleware
{
    public const string HeaderName = "X-Requested-With";

    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments("/api", StringComparison.Ordinal)
                && !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
                && !HttpMethods.IsOptions(request.Method)
                && string.IsNullOrEmpty(request.Headers[HeaderName]))
            {
                await Problems.CsrfHeaderMissing().ExecuteAsync(context);
                return;
            }

            await next(context);
        });
}
