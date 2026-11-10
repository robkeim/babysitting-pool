using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace BabysittingPool.Api;

/// <summary>
/// W3C trace correlation (ADR 0010): the <c>traceparent</c> trace id is adopted or created, put in the log scope,
/// echoed in <see cref="HeaderName"/> and quoted in every Problem, so an error shown to a Member names its log lines.
/// </summary>
internal static class Correlation
{
    public const string HeaderName = "X-Trace-Id";

    public const string ApiSource = "Api";

    public const string SettleSource = "Settle";

    private const string TraceParentHeader = "traceparent";

    private const string TraceIdItemKey = "babysitting.trace-id";

    private const int TraceIdLength = 32;

    private const int SpanIdLength = 16;

    /// <summary>The trace id of this request; the ASP.NET identifier only if the middleware did not run.</summary>
    public static string TraceId(this HttpContext context) =>
        context.Items.TryGetValue(TraceIdItemKey, out var traceId) && traceId is string text ? text : context.TraceIdentifier;

    /// <summary>The signed-in Member's opaque id, once authentication has run.</summary>
    public static string? MemberId(this HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string NewTraceId() => ActivityTraceId.CreateRandom().ToHexString();

    /// <summary>The trace id of a version 00 <c>traceparent</c> (<c>00-{32 hex}-{16 hex}-{2 hex}</c>); false when absent or malformed.</summary>
    public static bool TryParseTraceParent(string? header, out string traceId)
    {
        traceId = string.Empty;
        var parts = header?.Split('-');
        if (parts is not ["00", { Length: TraceIdLength } trace, { Length: SpanIdLength } span, { Length: 2 } flags]
            || !IsLowerHex(trace) || !IsLowerHex(span) || !IsLowerHex(flags)
            || IsAllZero(trace) || IsAllZero(span))
        {
            return false;
        }

        traceId = trace;
        return true;
    }

    /// <summary>Outermost, so the request line and everything below it carry the <c>TraceId</c>.</summary>
    public static IApplicationBuilder UseTraceId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Request.Headers[TraceParentHeader];
            var traceId = headers.Count == 1 && TryParseTraceParent(headers[0], out var adopted) ? adopted : NewTraceId();
            context.Items[TraceIdItemKey] = traceId;
            // OnStarting, because the exception handler clears the headers it finds.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = traceId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("TraceId", traceId))
            using (LogContext.PushProperty("Source", ApiSource))
            {
                await next(context);
            }
        });

    /// <summary>After authentication: adds <c>MemberId</c> to the scope of everything the Member's request logs.</summary>
    public static IApplicationBuilder UseMemberLogScope(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.MemberId() is not { } memberId)
            {
                await next(context);
                return;
            }

            using (LogContext.PushProperty("MemberId", memberId))
            {
                await next(context);
            }
        });

    private static bool IsLowerHex(string text) => text.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsAllZero(string text) => text.All(c => c == '0');
}
