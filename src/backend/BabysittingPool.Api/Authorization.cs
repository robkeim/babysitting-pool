using System.Security.Claims;
using System.Text.Encodings.Web;
using BabysittingPool.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BabysittingPool.Api;

/// <summary>
/// Deny by default (ADR 0014): endpoints call <c>RequireAuthorization</c> unless they are on the anonymous
/// allow-list, and <c>ApiAuthorizationTests</c> fails when one is neither.
/// </summary>
internal static class Authorization
{
    public const string Scheme = "Session";

    public const string AdminPolicy = "Admin";

    private const string AdminRole = "Admin";

    private const string MemberRole = "Member";

    private const string MemberItemKey = "babysitting.member";

    public static void AddSessionAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(Scheme, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(AdminRole));
    }

    /// <summary>The signed-in Member. Only valid on an endpoint that requires authorization.</summary>
    public static MemberView CurrentMember(this HttpContext context) =>
        (MemberView)context.Items[MemberItemKey]!;

    private sealed class SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ResolveSessionHandler resolveSession) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (SessionCookie.Read(Context) is not { Length: > 0 } secret
                || await resolveSession.HandleAsync(secret, Context.RequestAborted) is not { } member)
            {
                return AuthenticateResult.NoResult();
            }

            Context.Items[MemberItemKey] = member;
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
                    new Claim(ClaimTypes.Role, member.IsAdmin ? AdminRole : MemberRole),
                ],
                Authorization.Scheme);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Authorization.Scheme));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
            Problems.Unauthenticated().ExecuteAsync(Context);

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
            Problems.NotAdmin().ExecuteAsync(Context);
    }
}
