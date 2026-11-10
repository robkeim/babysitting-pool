using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace BabysittingPool.Tests.Api;

public class ClientLogEndpointTests : IDisposable
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private readonly CapturedLogEvents _logs = new();
    private readonly ApiFactory _factory;

    public ClientLogEndpointTests() =>
        _factory = new ApiFactory(configureServices: services => services.AddSingleton<ILogEventSink>(_logs));

    public void Dispose() => _factory.Dispose();

    private static object Valid(string? traceId = TraceId) => new
    {
        message = "Could not load requests",
        code = "request_list_failed",
        route = "/requests/:id",
        traceId,
        appVersion = "1.4.0",
    };

    private async Task<HttpClient> MemberAsync() => _factory.CreateMemberClient(await _factory.SeedMemberAsync());

    private LogEvent Single(string template) =>
        _logs.Events.Single(e => e.MessageTemplate.Text.StartsWith(template, StringComparison.Ordinal));

    [Fact]
    public async Task A_valid_report_is_logged_as_a_Frontend_event_under_the_reported_trace_id()
    {
        using var client = await MemberAsync();

        var response = await client.PostAsJsonAsync("/api/client-logs", Valid());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var logged = Single("ClientErrorReported");
        CapturedLogEvents.Text(logged, "Source").Should().Be("Frontend");
        CapturedLogEvents.Text(logged, "TraceId").Should().Be(TraceId);
        CapturedLogEvents.Text(logged, "ErrorCode").Should().Be("request_list_failed");
        CapturedLogEvents.Text(logged, "Route").Should().Be("/requests/:id");
        CapturedLogEvents.Text(logged, "AppVersion").Should().Be("1.4.0");
        CapturedLogEvents.Text(logged, "ErrorMessage").Should().Be("Could not load requests");
        CapturedLogEvents.Text(logged, "MemberId").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_report_without_a_trace_id_keeps_the_requests_own()
    {
        using var client = await MemberAsync();

        (await client.PostAsJsonAsync("/api/client-logs", Valid(traceId: null))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        CapturedLogEvents.Text(Single("ClientErrorReported"), "TraceId").Should().NotBeNullOrEmpty().And.NotBe(TraceId);
    }

    [Fact]
    public async Task Rejects_a_request_without_a_session_with_401()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.PostAsJsonAsync("/api/client-logs", Valid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _logs.Events.Should().NotContain(e => e.MessageTemplate.Text.StartsWith("ClientErrorReported", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_a_request_without_the_CSRF_header_with_403()
    {
        using var client = await MemberAsync();
        client.DefaultRequestHeaders.Remove("X-Requested-With");

        var response = await client.PostAsJsonAsync("/api/client-logs", Valid());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("csrf_header_missing");
    }

    [Fact]
    public async Task Rejects_an_oversized_body_with_413_and_logs_a_warning_without_the_content()
    {
        using var client = await MemberAsync();
        var padding = "OVERSIZED-PROBE-" + new string('x', 5000);

        var response = await client.PostAsJsonAsync("/api/client-logs", new { message = padding, code = "c", route = "/", appVersion = "1" });

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        Single("ClientLogRejected").Level.Should().Be(LogEventLevel.Warning);
        _logs.AllText().Should().NotContain("OVERSIZED-PROBE");
    }

    [Fact]
    public async Task Rejects_a_chunked_oversized_body_too()
    {
        using var client = await MemberAsync();
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 5000) + "{}")));
        content.Headers.ContentType = new("application/json");
        content.Headers.ContentLength = null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/client-logs") { Content = content };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    public static TheoryData<string, string> Invalid() => new()
    {
        { """{"message":"m","code":"c","route":"/","appVersion":"1","extra":"SCHEMA-PROBE"}""", "body" },
        { """{"message":"m","code":"c","route":"/","appVersion":"1","context":{"user":"SCHEMA-PROBE"}}""", "body" },
        { """{"message":{"a":"SCHEMA-PROBE"},"code":"c","route":"/","appVersion":"1"}""", "body" },
        { "not json SCHEMA-PROBE", "body" },
        { "null", "body" },
        { """{"code":"c","route":"/","appVersion":"1"}""", "message" },
        { """{"message":"  ","code":"c","route":"/","appVersion":"1"}""", "message" },
        { """{"message":"line one\nINJECTED SCHEMA-PROBE","code":"c","route":"/","appVersion":"1"}""", "message" },
        { """{"message":"line\u2028INJECTED SCHEMA-PROBE","code":"c","route":"/","appVersion":"1"}""", "message" },
        { """{"message":"fake\u202Eline SCHEMA-PROBE","code":"c","route":"/","appVersion":"1"}""", "message" },
        { """{"message":"m","code":"has space","route":"/","appVersion":"1"}""", "code" },
        { """{"message":"m","code":"c","route":"/?token=SCHEMA-PROBE","appVersion":"1"}""", "route" },
        { """{"message":"m","code":"c","route":"/","traceId":"NOT-A-TRACE","appVersion":"1"}""", "traceId" },
        { """{"message":"m","code":"c","route":"/"}""", "appVersion" },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task Rejects_a_payload_outside_the_schema_with_400_and_logs_a_warning_without_the_content(string json, string field)
    {
        using var client = await MemberAsync();

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/client-logs", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("invalid_request");
        problem.GetProperty("errors").EnumerateObject().Single().Name.Should().Be(field);
        Single("ClientLogRejected").Level.Should().Be(LogEventLevel.Warning);
        _logs.Events.Should().NotContain(e => e.MessageTemplate.Text.StartsWith("ClientErrorReported", StringComparison.Ordinal));
        _logs.AllText().Should().NotContain("SCHEMA-PROBE").And.NotContain("INJECTED");
    }

    [Fact]
    public async Task Accepts_a_message_of_exactly_the_maximum_length()
    {
        using var client = await MemberAsync();

        var response = await client.PostAsJsonAsync("/api/client-logs", new { message = new string('m', 500), code = "c", route = "/", appVersion = "1" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
