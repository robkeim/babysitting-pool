using System.Net;
using AwesomeAssertions;
using backend.tests.Support;

namespace backend.tests.Api;

/// <summary>Browser hardening headers (ADR 0014).</summary>
public class SecurityHeadersTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static void AssertHardened(HttpResponseMessage response)
    {
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Be("default-src 'self'; frame-ancestors 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    [Fact]
    public async Task Sets_the_hardening_headers_and_no_store_on_api_responses()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var response = await client.GetAsync("/api/me/account");

        AssertHardened(response);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Sets_the_hardening_headers_on_rejected_api_calls()
    {
        using var client = _factory.CreateClient();

        var unauthenticated = await client.GetAsync("/api/me/account");
        var missingCsrfHeader = await client.PostAsync("/api/requests", null);

        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertHardened(unauthenticated);
        unauthenticated.Headers.CacheControl!.NoStore.Should().BeTrue();
        missingCsrfHeader.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertHardened(missingCsrfHeader);
        missingCsrfHeader.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Does_not_mark_non_api_responses_no_store()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        AssertHardened(response);
        response.Headers.CacheControl.Should().BeNull();
    }

    [Fact]
    public async Task Registers_no_CORS_policy()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Sends_HSTS_in_Production_over_https_only()
    {
        using var production = new ApiFactory("Production", "admin@coop.example");
        using var secure = production.CreateClient(new() { BaseAddress = new Uri("https://coop.example") });
        using var secureE2E = _factory.CreateClient(new() { BaseAddress = new Uri("https://coop.example") });

        var prod = await secure.GetAsync("/health");
        var other = await secureE2E.GetAsync("/health");

        prod.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        other.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }
}
