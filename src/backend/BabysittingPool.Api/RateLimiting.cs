using System.Globalization;
using System.Net;
using System.Security.Claims;
using BabysittingPool.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

namespace BabysittingPool.Api;

/// <summary>
/// Rate limits (ADR 0014): counters in a SQLite file next to the database, so they survive a restart.
/// Over a limit is <c>429</c> ProblemDetails with <c>Retry-After</c>. The client IP comes from forwarded headers,
/// trusted only from the proxies listed in <c>ForwardedHeaders:KnownProxies</c>.
/// </summary>
internal static class RateLimiting
{
    public const string KnownProxiesSetting = "ForwardedHeaders:KnownProxies";

    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan MemberWindow = TimeSpan.FromMinutes(1);

    private const int SignInLinkLimit = 5;

    private const int TokenLimit = 10;

    private const int ClientLogLimit = 30;

    private const int MemberLimit = 120;

    private const int StreamLimit = 30;

    /// <summary>Sign-in link request, per email, so a Member's inbox cannot be flooded. Checked by the endpoint.</summary>
    public static Task<RateLimitDecision> HitSignInLinkEmailAsync(
        this SqliteRateLimitStore store, string email, CancellationToken cancellationToken) =>
        store.HitAsync($"sign-in-link-email:{email}", SignInLinkLimit, AuthWindow, cancellationToken);

    public static void AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = KnownProxies(configuration);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Only the configured proxies: the defaults would trust any loopback caller.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
    }

    /// <summary>Counts each request against its IP and Member limits; the first one over answers <c>429</c>.</summary>
    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var store = context.RequestServices.GetRequiredService<SqliteRateLimitStore>();
            foreach (var rule in new[] { ByIp(context), ByMember(context) })
            {
                if (rule is not { } limit)
                {
                    continue;
                }

                var decision = await store.HitAsync(limit.Key, limit.Limit, limit.Window, context.RequestAborted);
                if (!decision.Allowed)
                {
                    await Reject(context, decision.RetryAfter).ExecuteAsync(context);
                    return;
                }
            }

            await next();
        });

    /// <summary>
    /// Forwarded headers are honoured only when a proxy is configured: with no known proxy the middleware
    /// would trust every caller, and anyone could pick the IP they are limited under.
    /// </summary>
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this WebApplication app) =>
        KnownProxies(app.Configuration).Length == 0 ? app : app.UseForwardedHeaders();

    private static string[] KnownProxies(IConfiguration configuration) =>
        configuration.GetSection(KnownProxiesSetting).Get<string[]>() ?? [];

    /// <summary>A <c>429</c> for a limit the endpoint enforces itself.</summary>
    public static IResult Reject(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return Problems.TooManyRequests();
    }

    private readonly record struct Rule(string Key, int Limit, TimeSpan Window);

    private static Rule? ByIp(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method))
        {
            return null;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return request.Path.Value switch
        {
            "/api/auth/sign-in-links" => new Rule($"sign-in-link:{ip}", SignInLinkLimit, AuthWindow),
            "/api/invitations/accept" => new Rule($"redeem:{ip}", TokenLimit, AuthWindow),
            "/api/auth/sessions" => new Rule($"verify:{ip}", TokenLimit, AuthWindow),
            _ => null,
        };
    }

    private static Rule? ByMember(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } member
            || !path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            return null;
        }

        // One long-lived request, counted once when it opens. Its own bucket, so reconnecting is not starved by other
        // calls and a reconnect loop cannot eat the Member's budget for them.
        if (path.StartsWithSegments(NotificationEndpoints.StreamPath, StringComparison.Ordinal))
        {
            return new Rule($"notification-stream:{member}", StreamLimit, MemberWindow);
        }

        // Client logs are batched by the SPA, so a Member sending them faster than this is a bug or abuse.
        return path.StartsWithSegments("/api/client-logs", StringComparison.Ordinal)
            ? new Rule($"client-logs:{member}", ClientLogLimit, MemberWindow)
            : new Rule($"member:{member}", MemberLimit, MemberWindow);
    }
}
