using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace BabysittingPool.Tests.Logging;

/// <summary>One Information line per business outcome, found by <c>RequestId</c> (ADR 0010).</summary>
public sealed class BusinessOutcomeLoggingTests : IDisposable
{
    private readonly CapturedLogEvents _logs = new();
    private readonly ApiFactory _factory;

    public BusinessOutcomeLoggingTests() =>
        _factory = new ApiFactory(configureServices: services => services.AddSingleton<ILogEventSink>(_logs));

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Each_request_outcome_emits_exactly_one_information_event_carrying_its_request_id()
    {
        var requesterId = await _factory.SeedAccountAsync(Credits.FromDecimal(100m), Credits.FromQuarterCredits(0));
        var sitterId = await _factory.SeedAccountAsync(Credits.FromDecimal(0m), Credits.FromQuarterCredits(0));
        var adminId = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(adminId);
        using var requester = _factory.CreateMemberClient(requesterId);
        using var sitter = _factory.CreateMemberClient(sitterId);
        using var admin = _factory.CreateMemberClient(adminId);

        var withdrawn = await PostAsync(requester);
        await Ok(sitter.PostAsync($"/api/requests/{withdrawn}/claim", null));
        await Ok(sitter.PostAsync($"/api/requests/{withdrawn}/withdraw", null));
        await Ok(requester.PostAsync($"/api/requests/{withdrawn}/cancel", null));

        var completed = await PostAsync(requester);
        var expired = await PostAsync(requester);
        await Ok(sitter.PostAsync($"/api/requests/{completed}/claim", null));
        _factory.Clock.Advance(TimeSpan.FromHours(11));
        await Ok(admin.PostAsync("/api/admin/settle", null));

        var released = await PostAsync(requester);
        await Ok(admin.PostAsync($"/api/admin/members/{requesterId}/offboard", null));

        OutcomesOf(withdrawn).Should().Equal("RequestPosted", "RequestClaimed", "ClaimWithdrawn", "RequestCanceled");
        OutcomesOf(completed).Should().Equal("RequestPosted", "RequestClaimed", "RequestCompleted");
        OutcomesOf(expired).Should().Equal("RequestPosted", "RequestExpired");
        OutcomesOf(released).Should().Equal("RequestPosted", "RequestCanceled");
        _logs.Events.Where(e => e.MessageTemplate.Text.StartsWith("MemberOffboarded", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Level.Should().Be(LogEventLevel.Information);
    }

    private static bool IsOutcome(LogEvent e) => e.Level == LogEventLevel.Information && e.Properties.ContainsKey("RequestId");

    /// <summary>The names of the Information events logged for the Request, in order.</summary>
    private string[] OutcomesOf(string requestId) =>
        [.. _logs.Events
            .Where(e => IsOutcome(e) && CapturedLogEvents.Text(e, "RequestId") == requestId)
            .Select(e => e.MessageTemplate.Text.Split(' ')[0])];

    private async Task<string> PostAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/requests", new
        {
            startsAt = _factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 120,
            childCount = 1,
            location = "SittersHouse",
            transport = "None",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task Ok(Task<HttpResponseMessage> call) => (await call).EnsureSuccessStatusCode();
}
