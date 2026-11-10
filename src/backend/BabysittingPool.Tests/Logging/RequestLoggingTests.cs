using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Api;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace BabysittingPool.Tests.Logging;

public class RequestLoggingTests : IDisposable
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private readonly CapturedLogEvents _logs = new();
    private readonly ApiFactory _factory;

    public RequestLoggingTests() =>
        _factory = new ApiFactory(configureServices: services => services.AddSingleton<ILogEventSink>(_logs));

    public void Dispose() => _factory.Dispose();

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? traceparent = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (traceparent is not null)
        {
            request.Headers.Add("traceparent", traceparent);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task A_failing_requests_trace_id_is_in_the_log_line_the_response_header_and_the_problem_body()
    {
        using var client = _factory.CreateSpaClient();

        var response = await GetAsync(client, "/api/me/account", $"00-{TraceId}-00f067aa0ba902b7-01");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Trace-Id").Should().Equal(TraceId);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString().Should().Be(TraceId);
        var line = _logs.RequestLine("/api/me/account");
        CapturedLogEvents.Text(line, "TraceId").Should().Be(TraceId);
        CapturedLogEvents.Text(line, "StatusCode").Should().Be("401");
        CapturedLogEvents.Text(line, "Source").Should().Be("Api");
        CapturedLogEvents.Text(line, "RequestMethod").Should().Be("GET");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("00-00000000000000000000000000000000-00f067aa0ba902b7-01")]
    [InlineData("00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01")]
    public async Task Without_a_usable_traceparent_a_trace_id_is_created_and_still_agrees_everywhere(string? traceparent)
    {
        using var client = _factory.CreateSpaClient();

        var response = await GetAsync(client, "/api/me/account", traceparent);

        var header = response.Headers.GetValues("X-Trace-Id").Single();
        header.Should().MatchRegex("^[0-9a-f]{32}$").And.NotBe("00000000000000000000000000000000").And.NotBe(TraceId);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString().Should().Be(header);
        CapturedLogEvents.Text(_logs.RequestLine("/api/me/account"), "TraceId").Should().Be(header);
    }

    [Fact]
    public async Task Every_problem_carries_the_trace_id_including_a_rule_violation_raised_below_the_api()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(0m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsJsonAsync("/api/requests", new { startsAt = _factory.Clock.GetUtcNow().AddHours(10), durationMinutes = 120, childCount = 1, location = "SittersHouse", transport = "None" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var traceId = response.Headers.GetValues("X-Trace-Id").Single();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString().Should().Be(traceId);
        var line = _logs.RequestLine("/api/requests");
        CapturedLogEvents.Text(line, "TraceId").Should().Be(traceId);
        line.Level.Should().Be(LogEventLevel.Information, "an expected 4xx outcome is not an error (ADR 0006)");
        CapturedLogEvents.Text(line, "MemberId").Should().Be(memberId.ToString());
    }

    [Fact]
    public async Task The_request_line_names_the_route_template_and_never_the_raw_path_or_query()
    {
        var id = Guid.NewGuid();
        using var client = _factory.CreateSpaClient();

        await client.GetAsync("/api/not-a-route/PRIVATE-PATH");
        await client.PostAsync($"/api/requests/{id}/claim?q=PRIVATE-QUERY", null);

        var line = _logs.RequestLine("/api/requests/{id:guid}/claim");
        line.Properties.Keys.Should().NotContain(["RequestPath", "Path", "QueryString", "Query"]);
        _logs.RequestLine("unmatched").Properties.Keys.Should().NotContain("RequestPath");
        _logs.AllText().Should().NotContain(id.ToString()).And.NotContain("PRIVATE-QUERY").And.NotContain("PRIVATE-PATH");
    }

    [Fact]
    public async Task A_signed_in_members_id_is_in_the_scope_of_what_the_request_logs()
    {
        var memberId = await _factory.SeedMemberAsync();
        using var client = _factory.CreateMemberClient(memberId);

        await client.GetAsync("/api/me");

        CapturedLogEvents.Text(_logs.RequestLine("/api/me"), "MemberId").Should().Be(memberId.ToString());
    }

    [Fact]
    public async Task A_request_nobody_signed_in_has_no_member_id()
    {
        using var client = _factory.CreateSpaClient();

        await client.GetAsync("/api/me");

        _logs.RequestLine("/api/me").Properties.Should().NotContainKey("MemberId");
    }

    [Fact]
    public async Task An_unhandled_exception_is_logged_as_an_error_with_the_trace_id_and_answers_a_500_problem()
    {
        using var failing = new ApiFactory(configureServices: services =>
        {
            services.AddSingleton<ILogEventSink>(_logs);
            services.AddScoped<SettleHandler>(_ => throw new InvalidOperationException("boom"));
        });
        var adminId = await failing.SeedMemberAsync();
        await failing.MakeAdminAsync(adminId);
        using var client = failing.CreateMemberClient(adminId);

        var response = await client.PostAsync("/api/admin/settle", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var traceId = response.Headers.GetValues("X-Trace-Id").Single();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString().Should().Be(traceId);
        var line = _logs.RequestLine("/api/admin/settle");
        line.Level.Should().Be(LogEventLevel.Error);
        CapturedLogEvents.Text(line, "TraceId").Should().Be(traceId);
        var handlerLines = _logs.Events.Where(e => !e.MessageTemplate.Text.StartsWith("HttpRequestHandled", StringComparison.Ordinal)).ToList();
        handlerLines.Should().Contain(e => e.Level == LogEventLevel.Error, "the exception handler logs the failure");
        handlerLines.Where(e => e.Properties.ContainsKey("TraceId")).Should().OnlyContain(e => CapturedLogEvents.Text(e, "TraceId") == traceId);
        _logs.Events.Should().OnlyContain(e => !e.Properties.ContainsKey("RequestPath"));
    }

    [Fact]
    public async Task Each_settle_tick_is_its_own_trace_under_the_settle_source()
    {
        using var ticking = new ApiFactory(
            settings: new() { ["Settle:Enabled"] = "true" },
            configureServices: services => services.AddSingleton<ILogEventSink>(_logs));
        using var client = ticking.CreateSpaClient();
        await client.GetAsync("/health");

        await WaitForTicksAsync(1);
        ticking.Clock.Advance(SettleHostedService.Interval);
        await WaitForTicksAsync(2);

        var ticks = _logs.Events.Where(e => CapturedLogEvents.Text(e, "Source") == "Settle" && e.MessageTemplate.Text.StartsWith("SettleRan", StringComparison.Ordinal)).ToList();
        ticks.Select(e => CapturedLogEvents.Text(e, "TraceId")).Should().OnlyHaveUniqueItems().And.OnlyContain(id => id!.Length == 32);
    }

    private async Task WaitForTicksAsync(int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (_logs.Events.Count(e => e.MessageTemplate.Text.StartsWith("SettleRan", StringComparison.Ordinal)) < count)
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
