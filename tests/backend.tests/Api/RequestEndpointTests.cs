using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;

namespace backend.tests.Api;

public class RequestEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private object Body(int hoursAhead = 10, int durationMinutes = 120, string transport = "None") => new
    {
        startsAt = _factory.Clock.GetUtcNow().AddHours(hoursAhead),
        durationMinutes,
        childCount = 1,
        location = "SittersHouse",
        transport,
        lunchOrDinner = false,
        snack = false,
        area = "Riverside",
        address = "12 River Rd",
    };

    [Fact]
    public async Task Posting_returns_201_with_the_request_and_it_appears_in_my_requests_and_the_balance()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsJsonAsync("/api/requests", Body());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var posted = await response.Content.ReadFromJsonAsync<JsonElement>();
        posted.GetProperty("status").GetString().Should().Be("Posted");
        posted.GetProperty("costQuarterCredits").GetInt64().Should().Be(8);
        posted.GetProperty("claimCutoffAt").GetString().Should().Be("2026-03-01T16:00:00Z");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/me/requests");
        var item = list.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetString().Should().Be(posted.GetProperty("id").GetString());
        item.GetProperty("address").GetString().Should().Be("12 River Rd");
        var account = await client.GetFromJsonAsync<JsonElement>("/api/me/account");
        account.GetProperty("availableQuarterCredits").GetInt64().Should().Be(32);
        account.GetProperty("escrowedQuarterCredits").GetInt64().Should().Be(8);
    }

    [Fact]
    public async Task Other_members_never_see_a_requests_exact_address()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var other = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var otherClient = _factory.CreateMemberClient(other);
        (await ownerClient.PostAsJsonAsync("/api/requests", Body())).EnsureSuccessStatusCode();

        var list = await otherClient.GetFromJsonAsync<JsonElement>("/api/me/requests");

        list.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0, 5, "below_minimum_lead_time")]
    [InlineData(0, 0, 10, "no_available_credits")]
    [InlineData(7, 0, 10, "insufficient_credits")]
    public async Task Posting_a_blocked_request_answers_422_with_the_rule_code(
        long availableQuarters, long escrowedQuarters, int hoursAhead, string code)
    {
        // The lead-time case has a funded account; the others are the zero and short balances.
        var funded = code == "below_minimum_lead_time" ? 40 : availableQuarters;
        var memberId = await _factory.SeedAccountAsync(Credits.FromQuarterCredits(funded), Credits.FromQuarterCredits(escrowedQuarters));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsJsonAsync("/api/requests", Body(hoursAhead));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, code);
    }

    [Fact]
    public async Task Posting_a_duration_that_is_not_a_multiple_of_15_minutes_answers_422()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsJsonAsync("/api/requests", Body(durationMinutes: 82));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "invalid_duration");
    }

    [Fact]
    public async Task Posting_without_a_session_answers_401()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.PostAsJsonAsync("/api/requests", Body());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Listing_without_a_session_answers_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me/requests");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Quote_returns_the_calculators_cost_in_quarter_credits()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var quote = await client.GetFromJsonAsync<JsonElement>(
            "/api/requests/quote?durationMinutes=90&childCount=2&location=RequestingFamilysHouse&transport=None&lunchOrDinner=true&snack=false");

        // 1.5h x 1.50 + 0.50 premium + 2 children x 0.50 lunch = 3.75
        quote.GetProperty("costQuarterCredits").GetInt64().Should().Be(15);
    }

    [Fact]
    public async Task Quote_rejects_transport_with_the_family_house_premium_with_422()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var response = await client.GetAsync(
            "/api/requests/quote?durationMinutes=60&childCount=1&location=RequestingFamilysHouse&transport=OneWay");

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "transport_with_family_house");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
    }
}
