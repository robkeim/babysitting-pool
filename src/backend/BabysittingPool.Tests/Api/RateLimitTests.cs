using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Api;

/// <summary>Rate limits (ADR 0014). Each test gets a fresh host, so limiter state never leaks between tests.</summary>
public class RateLimitTests : IDisposable
{
    private const string ProxyIp = "10.0.0.1";

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>The same host, but the TCP peer is <see cref="ProxyIp"/> (TestServer has none) and optionally a trusted proxy.</summary>
    private WebApplicationFactory<BabysittingPool.E2E.E2EHost> Behind(bool trustProxy) =>
        _factory.WithWebHostBuilder(builder =>
        {
            if (trustProxy)
            {
                builder.UseSetting("ForwardedHeaders:KnownProxies:0", ProxyIp);
            }

            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, PeerAddressFilter>());
        });

    private static HttpClient SpaClient(WebApplicationFactory<BabysittingPool.E2E.E2EHost> factory, string? forwardedFor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        }

        return client;
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var retryAfter = response.Headers.RetryAfter!.Delta!.Value;
        retryAfter.Should().BeGreaterThan(TimeSpan.Zero);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(429);
        problem.GetProperty("code").GetString().Should().Be("rate_limited");
    }

    [Fact]
    public async Task Requesting_sign_in_links_is_limited_to_5_per_IP()
    {
        using var host = Behind(trustProxy: false);
        using var client = SpaClient(host);

        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = $"member{i}@example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        await AssertRateLimitedAsync(await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "member5@example.com" }));
    }

    [Fact]
    public async Task Requesting_sign_in_links_is_limited_to_5_per_email_whoever_asks_and_whether_or_not_it_is_a_Member()
    {
        using var host = Behind(trustProxy: true);

        for (var i = 0; i < 5; i++)
        {
            using var client = SpaClient(host, forwardedFor: $"203.0.113.{i}");
            (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "Nobody@Example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        using var sixth = SpaClient(host, forwardedFor: "203.0.113.50");
        await AssertRateLimitedAsync(await sixth.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "nobody@example.com" }));
        (await sixth.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "someone-else@example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Redeeming_an_invitation_is_limited_to_10_per_IP()
    {
        using var host = Behind(trustProxy: false);
        using var client = SpaClient(host);

        for (var i = 0; i < 10; i++)
        {
            (await client.PostAsJsonAsync("/api/invitations/accept", new { token = "nope", displayName = "Ada" }))
                .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        await AssertRateLimitedAsync(await client.PostAsJsonAsync("/api/invitations/accept", new { token = "nope", displayName = "Ada" }));
    }

    [Fact]
    public async Task Verifying_a_sign_in_link_is_limited_to_10_per_IP()
    {
        using var host = Behind(trustProxy: false);
        using var client = SpaClient(host);

        for (var i = 0; i < 10; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/sessions", new { token = "nope" }))
                .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        await AssertRateLimitedAsync(await client.PostAsJsonAsync("/api/auth/sessions", new { token = "nope" }));
        // The limits are per endpoint: redeeming an Invitation still has its own allowance.
        (await client.PostAsJsonAsync("/api/invitations/accept", new { token = "nope", displayName = "Ada" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Forwarded_headers_are_ignored_unless_they_come_from_the_configured_proxy()
    {
        using var untrusted = Behind(trustProxy: false);

        for (var i = 0; i < 5; i++)
        {
            using var client = SpaClient(untrusted, forwardedFor: $"203.0.113.{i}");
            (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = $"member{i}@example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        // A spoofed address does not buy a fresh allowance.
        using var spoofed = SpaClient(untrusted, forwardedFor: "203.0.113.99");
        await AssertRateLimitedAsync(await spoofed.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "member9@example.com" }));

        // Behind the trusted proxy the same sixth caller, on its own address, gets through.
        using var trusted = Behind(trustProxy: true);
        for (var i = 0; i < 6; i++)
        {
            using var client = SpaClient(trusted, forwardedFor: $"203.0.113.{i}");
            (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = $"member{i}@example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public async Task A_trusted_network_and_forwarded_for_header_name_set_the_client_IP()
    {
        // Fly's proxy sits in a network, not at one address, and sends the client in Fly-Client-IP (ADR 0018).
        using var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/24");
            builder.UseSetting("ForwardedHeaders:ForwardedForHeaderName", "Fly-Client-IP");
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, PeerAddressFilter>());
        });

        for (var i = 0; i < 6; i++)
        {
            using var client = host.CreateClient();
            client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
            client.DefaultRequestHeaders.Add("Fly-Client-IP", $"203.0.113.{i}");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.1");
            (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = $"member{i}@example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public async Task Client_logs_are_limited_to_30_per_minute_per_Member()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        using var other = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        for (var i = 0; i < 30; i++)
        {
            (await client.PostAsJsonAsync("/api/client-logs", new { })).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        await AssertRateLimitedAsync(await client.PostAsJsonAsync("/api/client-logs", new { }));
        (await other.PostAsJsonAsync("/api/client-logs", new { })).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Authenticated_calls_are_limited_to_120_per_minute_per_Member()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        using var other = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        for (var i = 0; i < 120; i++)
        {
            (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await AssertRateLimitedAsync(await client.GetAsync("/api/me"));
        (await other.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_reads_are_not_limited()
    {
        using var client = _factory.CreateClient();

        for (var i = 0; i < 130; i++)
        {
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private sealed class PeerAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ProxyIp);
                    await nextMiddleware();
                });
                next(app);
            };
    }
}
