using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;

namespace backend.tests.Api;

public class AccountEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Returns_the_current_members_balance_and_ledger_as_quarter_credits()
    {
        var memberId = await _factory.SeedAccountAsync(
            Credits.FromDecimal(10.25m), Credits.FromDecimal(2m), Credits.FromDecimal(10.25m));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.GetAsync("/api/me/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("availableQuarterCredits").GetInt64().Should().Be(41);
        root.GetProperty("escrowedQuarterCredits").GetInt64().Should().Be(8);
        var ledger = root.GetProperty("ledger");
        ledger.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        var entry = ledger.GetProperty("items").EnumerateArray().Single();
        Guid.TryParse(entry.GetProperty("id").GetString(), out _).Should().BeTrue();
        entry.GetProperty("kind").GetString().Should().Be("Grant");
        entry.GetProperty("amountQuarterCredits").GetInt64().Should().Be(41);
        entry.GetProperty("occurredAt").GetString().Should().Be("2026-03-01T09:00:00Z");
    }

    [Fact]
    public async Task Rejects_a_request_that_names_no_member_with_401_problem_json()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me/account");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Answers_404_problem_json_when_the_member_has_no_account()
    {
        using var client = _factory.CreateMemberClient(Guid.NewGuid());

        var response = await client.GetAsync("/api/me/account");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "account_not_found");
    }

    [Fact]
    public async Task Pages_the_ledger_with_limit_and_cursor_query_parameters()
    {
        var memberId = await _factory.SeedAccountAsync(
            Credits.FromDecimal(3m), Credits.FromQuarterCredits(0),
            Credits.FromDecimal(1m), Credits.FromDecimal(1m), Credits.FromDecimal(1m));
        using var client = _factory.CreateMemberClient(memberId);

        var first = await GetLedgerAsync(client, "/api/me/account?limit=2");
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await GetLedgerAsync(client, $"/api/me/account?limit=2&cursor={Uri.EscapeDataString(cursor!)}");

        first.GetProperty("items").GetArrayLength().Should().Be(2);
        second.GetProperty("items").GetArrayLength().Should().Be(1);
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Rejects_a_malformed_cursor_with_400_problem_json_naming_the_field()
    {
        var memberId = await _factory.SeedAccountAsync(
            Credits.FromDecimal(1m), Credits.FromQuarterCredits(0), Credits.FromDecimal(1m));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.GetAsync("/api/me/account?cursor=not-a-cursor");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_request");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").GetProperty("cursor")[0].GetString().Should().Be("invalid");
    }

    [Fact]
    public async Task Development_seeds_a_dev_member_with_the_ten_credit_starting_balance()
    {
        using var development = new ApiFactory("Development");
        using var client = development.CreateMemberClient(Guid.Parse("00000000-0000-0000-0000-00000000de01"));

        var json = await client.GetFromJsonAsync<JsonElement>("/api/me/account");

        json.GetProperty("availableQuarterCredits").GetInt64().Should().Be(40);
        json.GetProperty("escrowedQuarterCredits").GetInt64().Should().Be(0);
        var entry = json.GetProperty("ledger").GetProperty("items").EnumerateArray().Single();
        entry.GetProperty("kind").GetString().Should().Be("Grant");
        entry.GetProperty("amountQuarterCredits").GetInt64().Should().Be(40);
    }

    private static async Task<JsonElement> GetLedgerAsync(HttpClient client, string url)
    {
        var json = await client.GetFromJsonAsync<JsonElement>(url);
        return json.GetProperty("ledger");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
    }
}
