using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace BabysittingPool.Api;

/// <summary>
/// Rate limits (ADR 0014): the built-in limiter with in-memory state (one instance; a persistent store is #54).
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

    /// <summary>Sign-in link request, per email, so a Member's inbox cannot be flooded. Checked by the endpoint.</summary>
    public static PartitionedRateLimiter<string> CreateSignInLinkEmailLimiter() =>
        PartitionedRateLimiter.Create<string, string>(
            email => Fixed(email, SignInLinkLimit, AuthWindow));

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

        services.AddSingleton(_ => CreateSignInLinkEmailLimiter());
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(ByIp),
                PartitionedRateLimiter.Create<HttpContext, string>(ByMember));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? delay : AuthWindow;
                await Reject(context.HttpContext, retryAfter).ExecuteAsync(context.HttpContext);
            };
        });
    }

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

    private static RateLimitPartition<string> ByIp(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method))
        {
            return RateLimitPartition.GetNoLimiter("none");
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return request.Path.Value switch
        {
            "/api/auth/sign-in-links" => Fixed($"sign-in-link:{ip}", SignInLinkLimit, AuthWindow),
            "/api/invitations/accept" => Fixed($"redeem:{ip}", TokenLimit, AuthWindow),
            "/api/auth/sessions" => Fixed($"verify:{ip}", TokenLimit, AuthWindow),
            _ => RateLimitPartition.GetNoLimiter("none"),
        };
    }

    private static RateLimitPartition<string> ByMember(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } member
            || !path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            return RateLimitPartition.GetNoLimiter("none");
        }

        // Client logs are batched by the SPA, so a Member sending them faster than this is a bug or abuse.
        return path.StartsWithSegments("/api/client-logs", StringComparison.Ordinal)
            ? Fixed($"client-logs:{member}", ClientLogLimit, MemberWindow)
            : Fixed($"member:{member}", MemberLimit, MemberWindow);
    }

    private static RateLimitPartition<string> Fixed(string key, int limit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = window,
            QueueLimit = 0,
        });
}
