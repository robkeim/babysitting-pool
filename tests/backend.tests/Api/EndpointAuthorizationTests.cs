using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using backend.tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Api;

/// <summary>Deny by default (ADR 0014): every <c>/api</c> endpoint requires a session unless it is on the allow-list.</summary>
public class EndpointAuthorizationTests : IDisposable
{
    /// <summary>The only <c>/api</c> endpoints that answer without a session. Adding one is a security decision.</summary>
    private static readonly string[] AnonymousAllowList =
    [
        "POST /api/invitations/preview",
        "POST /api/invitations/accept",
        "POST /api/auth/sign-in-links",
        "POST /api/auth/sessions",
        // Signing out must clear the cookie even when the session has already expired.
        "DELETE /api/auth/sessions/current",
    ];

    // Production, so the Development and E2E endpoints (/api/dev, /api/test) are not mapped and cannot hide a gap.
    private readonly ApiFactory _factory = new("Production", bootstrapAdminEmail: "admin@example.test");

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Every_api_endpoint_requires_authorization_or_is_on_the_anonymous_allow_list()
    {
        var endpoints = ApiEndpoints();

        endpoints.Should().NotBeEmpty();
        var anonymous = endpoints.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null).Select(Describe);
        anonymous.Should().BeEquivalentTo(AnonymousAllowList, "only the allow-list may skip authorization");
        var unprotected = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(Describe);
        unprotected.Should().BeEmpty("every other endpoint must call RequireAuthorization");
    }

    [Fact]
    public void Every_admin_endpoint_sits_behind_the_Admin_policy()
    {
        var admin = ApiEndpoints().Where(e => e.RoutePattern.RawText!.StartsWith("/api/admin", StringComparison.Ordinal));

        admin.Should().OnlyContain(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "Admin"));
    }

    [Fact]
    public async Task The_Admin_policy_admits_the_Admin_and_turns_a_Member_away()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(Principal("Admin"), null, "Admin")).Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(Principal("Member"), null, "Admin")).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_missing_session_answers_401_on_a_protected_endpoint()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.GetAsync("/api/me/account");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    private RouteEndpoint[] ApiEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal))
            .ToArray();

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join('|', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {endpoint.RoutePattern.RawText}";

    private static ClaimsPrincipal Principal(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Session"));
}
