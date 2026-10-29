using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Application.Addresses;
using BabysittingPool.Infrastructure;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace backend.tests.Api;

/// <summary>GET /api/address-suggestions (ADR 0020). BAN is a fake handler; nothing here calls the real service.</summary>
public class AddressSuggestionTests : IDisposable
{
    private const string Secret = "12 impasse Zorglub";

    private const string Features = """
        {"features":[
          {"properties":{"id":"80021_6115_12","label":"12 Rue de la Paix 80000 Amiens","type":"housenumber","city":"Amiens","citycode":"80021","depcode":"80","postcode":"80000"}},
          {"properties":{"id":"80021_6115","label":"Rue de la Paix 80000 Amiens","type":"street","city":"Amiens","citycode":"80021","depcode":"80","postcode":"80000"}},
          {"properties":{"id":"80021","label":"Amiens","type":"municipality","city":"Amiens","citycode":"80021","depcode":"80","postcode":"80000"}},
          {"properties":{"id":"80001_x","label":"La Ferme 80100 Abbeville","type":"locality","city":"Abbeville","citycode":"80001","depcode":"80","postcode":"80100"}}
        ]}
        """;

    private readonly FakeBan _ban = new();
    private readonly CapturedLogs _logs = new();
    private readonly ApiFactory _factory;

    public AddressSuggestionTests()
    {
        _factory = new ApiFactory(configureServices: services =>
        {
            services.AddHttpClient<IAddressSuggester, BanAddressSuggester>()
                .ConfigurePrimaryHttpMessageHandler(() => _ban);
            services.AddLogging(logging => logging.AddProvider(_logs));
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        _ban.Dispose();
        _logs.Dispose();
    }

    private HttpClient MemberClient(Guid member) => _factory.CreateMemberClient(member);

    [Fact]
    public async Task Signed_out_callers_get_401()
    {
        using var client = _factory.CreateSpaClient();

        (await client.GetAsync("/api/address-suggestions?q=rue")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Member_gets_housenumber_and_locality_suggestions_only()
    {
        _ban.Respond(HttpStatusCode.OK, Features);
        using var client = MemberClient(await _factory.SeedMemberAsync());

        var page = await client.GetFromJsonAsync<JsonElement>("/api/address-suggestions?q=12%20rue%20de%20la%20paix");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("type").GetString()).Should().Equal("housenumber", "locality");
        items[0].GetProperty("banId").GetString().Should().Be("80021_6115_12");
        items[0].GetProperty("label").GetString().Should().Be("12 Rue de la Paix 80000 Amiens");
        items[0].GetProperty("commune").GetString().Should().Be("Amiens");
        items[0].GetProperty("inseeCode").GetString().Should().Be("80021");
        items[0].GetProperty("departmentNumber").GetString().Should().Be("80");
        items[0].GetProperty("postcode").GetString().Should().Be("80000");
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        _ban.Requests.Single().Query.Should().Contain("autocomplete=1").And.Contain("q=12%20rue%20de%20la%20paix");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("%20%20a%20%20")]
    public async Task A_query_shorter_than_3_characters_is_400_and_never_reaches_BAN(string q)
    {
        using var client = MemberClient(await _factory.SeedMemberAsync());

        var response = await client.GetAsync($"/api/address-suggestions?q={q}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("invalid_request");
        _ban.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_missing_query_is_400()
    {
        using var client = MemberClient(await _factory.SeedMemberAsync());

        (await client.GetAsync("/api/address-suggestions")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_BAN_failure_is_503(HttpStatusCode status)
    {
        _ban.Respond(status, "");
        using var client = MemberClient(await _factory.SeedMemberAsync());

        await AssertUnavailableAsync(await client.GetAsync("/api/address-suggestions?q=rue"));
    }

    [Fact]
    public async Task A_BAN_timeout_is_503()
    {
        _ban.Throw(new TaskCanceledException("timed out"));
        using var client = MemberClient(await _factory.SeedMemberAsync());

        await AssertUnavailableAsync(await client.GetAsync("/api/address-suggestions?q=rue"));
    }

    [Fact]
    public async Task A_connection_failure_is_503()
    {
        _ban.Throw(new HttpRequestException($"no route to https://x/search?q={Secret}"));
        using var client = MemberClient(await _factory.SeedMemberAsync());

        await AssertUnavailableAsync(await client.GetAsync("/api/address-suggestions?q=rue"));
    }

    [Fact]
    public async Task A_garbled_BAN_body_is_503()
    {
        _ban.Respond(HttpStatusCode.OK, "<html>maintenance</html>");
        using var client = MemberClient(await _factory.SeedMemberAsync());

        await AssertUnavailableAsync(await client.GetAsync("/api/address-suggestions?q=rue"));
    }

    [Fact]
    public async Task The_query_text_is_never_logged_even_when_BAN_fails()
    {
        _ban.Throw(new HttpRequestException($"no route to https://x/search?q={Secret}"));
        using var client = MemberClient(await _factory.SeedMemberAsync());
        await client.GetAsync($"/api/address-suggestions?q={Uri.EscapeDataString(Secret)}");
        _ban.Respond(HttpStatusCode.OK, Features);
        await client.GetAsync($"/api/address-suggestions?q={Uri.EscapeDataString(Secret)}");

        _logs.Messages.Should().NotBeEmpty("the capture must be wired, or this test proves nothing");
        _logs.Messages.Should().NotContain(m => m.Contains("Zorglub", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_Member_is_limited_to_60_suggestion_calls_a_minute()
    {
        _ban.Respond(HttpStatusCode.OK, Features);
        using var client = MemberClient(await _factory.SeedMemberAsync());
        using var other = MemberClient(await _factory.SeedMemberAsync());

        for (var i = 0; i < 60; i++)
        {
            (await client.GetAsync("/api/address-suggestions?q=rue")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var limited = await client.GetAsync("/api/address-suggestions?q=rue");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        (await other.GetAsync("/api/address-suggestions?q=rue")).StatusCode.Should().Be(HttpStatusCode.OK, "the limit is per Member");
    }

    private static async Task AssertUnavailableAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("address_service_unavailable");
    }

    private sealed class FakeBan : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _next = () => new HttpResponseMessage(HttpStatusCode.OK);

        public List<(string Query, string Path)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) =>
            _next = () => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        public void Throw(Exception exception) => _next = () => throw exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.Query, request.RequestUri.AbsolutePath));
            return Task.FromResult(_next());
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Capture(this);

        public void Dispose()
        {
        }

        private sealed class Capture(CapturedLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._messages)
                {
                    owner._messages.Add($"{formatter(state, exception)} {exception}");
                }
            }
        }
    }
}
