using System.Net;
using AwesomeAssertions;
using backend.tests.Support;

namespace backend.tests.Api;

/// <summary>The API serves the built SPA on its own origin (ADR 0018), under the hardening of ADR 0014.</summary>
public sealed class SpaHostingTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>spa</title>";

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"babysitting-spa-{Guid.NewGuid():N}");
    private readonly ApiFactory _factory;

    public SpaHostingTests()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app-abc123.js"), "console.log(1)");
        _factory = new ApiFactory(settings: new() { ["webroot"] = _webRoot });
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/requests/123")]
    [InlineData("/sign-in")]
    [InlineData("/apiary")]
    [InlineData("/healthy")]
    public async Task Falls_back_to_index_html_for_client_routes(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(IndexHtml);
    }

    [Theory]
    [InlineData("/api/nope")]
    [InlineData("/api")]
    [InlineData("/health/nope")]
    public async Task Does_not_fall_back_for_api_and_health(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("<title>spa</title>");
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task Still_serves_health_and_the_api()
    {
        using var client = _factory.CreateClient();

        var health = await client.GetAsync("/health");
        var api = await client.GetAsync("/api/me/account");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        health.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        api.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        api.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/requests/123")]
    [InlineData("/assets/app-abc123.js")]
    public async Task Static_files_carry_the_hardening_headers(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Be("default-src 'self'; frame-ancestors 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    [Fact]
    public async Task Caches_hashed_assets_forever_and_revalidates_the_page()
    {
        using var client = _factory.CreateClient();

        var asset = await client.GetAsync("/assets/app-abc123.js");
        var index = await client.GetAsync("/");
        var route = await client.GetAsync("/requests/123");

        asset.Headers.CacheControl!.Public.Should().BeTrue();
        asset.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(365));
        asset.Headers.CacheControl.ToString().Should().Contain("immutable");
        index.Headers.CacheControl!.NoCache.Should().BeTrue();
        route.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task Serves_no_spa_when_wwwroot_has_no_index_html()
    {
        using var bare = new ApiFactory();
        using var client = bare.CreateClient();

        var response = await client.GetAsync("/sign-in");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
