using BabysittingPool.Application.Identity;

namespace BabysittingPool.Api;

/// <summary>The session cookie (ADR 0012) and the Member behind it.</summary>
internal static class SessionCookie
{
    public const string Name = "session";

    /// <summary>The browser keeps the cookie long; the server alone decides when a session has gone idle.</summary>
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    public static void Set(HttpContext context, string secret)
    {
        context.Response.Cookies.Append(Name, secret, Options(context));
    }

    public static void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(Name, Options(context));
    }

    public static string? Read(HttpContext context) => context.Request.Cookies[Name];

    private static CookieOptions Options(HttpContext context)
    {
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var settings = context.RequestServices.GetRequiredService<SessionCookieSettings>();
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = CookieLifetime,
            // Safari drops Secure cookies on http://localhost, which is how Development and E2E are served (<see cref="SessionCookieSettings"/>).
            Secure = !environment.IsDevelopment() && !settings.PlainHttp,
        };
    }
}

/// <summary>Whether the session cookie is served over plain HTTP, as the E2E host does.</summary>
internal sealed record SessionCookieSettings(bool PlainHttp);
