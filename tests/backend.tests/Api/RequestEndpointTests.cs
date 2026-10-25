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
        lunch = false,
        snack = false,
        dinner = false,
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
    public async Task Open_requests_show_other_members_the_rough_area_but_never_the_address_or_their_own_requests()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var browser = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var browserClient = _factory.CreateMemberClient(browser);
        (await ownerClient.PostAsJsonAsync("/api/requests", Body())).EnsureSuccessStatusCode();

        var seen = await browserClient.GetFromJsonAsync<JsonElement>("/api/requests/open");
        var own = await ownerClient.GetFromJsonAsync<JsonElement>("/api/requests/open");

        var item = seen.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("area").GetString().Should().Be("Riverside");
        item.GetProperty("costQuarterCredits").GetInt64().Should().Be(8);
        item.TryGetProperty("address", out _).Should().BeFalse();
        item.TryGetProperty("requesterId", out _).Should().BeFalse();
        own.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Claiming_answers_200_with_the_address_and_moves_the_request_to_my_claims_and_off_the_open_list()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var sitter = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var sitterClient = _factory.CreateMemberClient(sitter);
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        var id = posted.GetProperty("id").GetString();

        var response = await sitterClient.PostAsync($"/api/requests/{id}/claim", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var claimed = await response.Content.ReadFromJsonAsync<JsonElement>();
        claimed.GetProperty("status").GetString().Should().Be("Claimed");
        claimed.GetProperty("address").GetString().Should().Be("12 River Rd");
        var claims = await sitterClient.GetFromJsonAsync<JsonElement>("/api/me/claims");
        claims.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetString().Should().Be(id);
        (await sitterClient.GetFromJsonAsync<JsonElement>("/api/requests/open")).GetProperty("items").GetArrayLength().Should().Be(0);
        var mine = await ownerClient.GetFromJsonAsync<JsonElement>("/api/me/requests");
        mine.GetProperty("items").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Claimed");
    }

    [Fact]
    public async Task A_second_claim_on_the_same_request_answers_409()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var first = await _factory.SeedMemberAsync();
        var second = await _factory.SeedMemberAsync();
        using var ownerClient = _factory.CreateMemberClient(owner);
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        var id = posted.GetProperty("id").GetString();
        using var firstClient = _factory.CreateMemberClient(first);
        using var secondClient = _factory.CreateMemberClient(second);
        (await firstClient.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();

        var response = await secondClient.PostAsync($"/api/requests/{id}/claim", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task Claiming_your_own_request_answers_422()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(owner);
        var posted = await (await client.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();

        var response = await client.PostAsync($"/api/requests/{posted.GetProperty("id").GetString()}/claim", null);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "own_request");
    }

    [Fact]
    public async Task Claiming_after_the_claim_cutoff_answers_422()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var sitterClient = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        _factory.Clock.Advance(TimeSpan.FromHours(7));

        var response = await sitterClient.PostAsync($"/api/requests/{posted.GetProperty("id").GetString()}/claim", null);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "claim_cutoff_passed");
    }

    [Fact]
    public async Task Claiming_an_unknown_request_answers_404()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var response = await client.PostAsync($"/api/requests/{Guid.NewGuid()}/claim", null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "request_not_found");
    }

    [Fact]
    public async Task Cancelling_answers_200_refunds_the_requester_and_a_second_cancel_answers_409()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(owner);
        var posted = await (await client.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        var id = posted.GetProperty("id").GetString();

        var response = await client.PostAsync($"/api/requests/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Cancelled");
        var account = await client.GetFromJsonAsync<JsonElement>("/api/me/account");
        account.GetProperty("availableQuarterCredits").GetInt64().Should().Be(40);
        account.GetProperty("escrowedQuarterCredits").GetInt64().Should().Be(0);
        await AssertProblemAsync(await client.PostAsync($"/api/requests/{id}/cancel", null), HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task Cancelling_someone_elses_request_or_an_unknown_one_answers_404()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var otherClient = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();

        var others = await otherClient.PostAsync($"/api/requests/{posted.GetProperty("id").GetString()}/cancel", null);
        var unknown = await otherClient.PostAsync($"/api/requests/{Guid.NewGuid()}/cancel", null);

        await AssertProblemAsync(others, HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "request_not_found");
    }

    [Fact]
    public async Task Withdrawing_answers_200_without_the_address_and_reopens_the_request()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var sitterClient = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        var id = posted.GetProperty("id").GetString();
        (await sitterClient.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();

        var response = await sitterClient.PostAsync($"/api/requests/{id}/withdraw", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var withdrawn = await response.Content.ReadFromJsonAsync<JsonElement>();
        withdrawn.GetProperty("status").GetString().Should().Be("Posted");
        withdrawn.GetProperty("address").GetString().Should().BeEmpty();
        (await sitterClient.GetFromJsonAsync<JsonElement>("/api/me/claims")).GetProperty("items").GetArrayLength().Should().Be(0);
        (await sitterClient.GetFromJsonAsync<JsonElement>("/api/requests/open")).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Withdrawing_a_claim_that_is_not_yours_answers_404()
    {
        var owner = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var ownerClient = _factory.CreateMemberClient(owner);
        using var sitterClient = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        using var otherClient = _factory.CreateMemberClient(await _factory.SeedMemberAsync());
        var posted = await (await ownerClient.PostAsJsonAsync("/api/requests", Body())).Content.ReadFromJsonAsync<JsonElement>();
        var id = posted.GetProperty("id").GetString();
        (await sitterClient.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();

        await AssertProblemAsync(await otherClient.PostAsync($"/api/requests/{id}/withdraw", null), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await ownerClient.PostAsync($"/api/requests/{id}/withdraw", null), HttpStatusCode.NotFound, "request_not_found");
    }

    [Theory]
    [InlineData("GET", "/api/requests/open")]
    [InlineData("GET", "/api/me/claims")]
    [InlineData("POST", "/api/requests/6f1c2b1e-0a52-4f43-9d4f-2f2f6f1c2b1e/claim")]
    [InlineData("POST", "/api/requests/6f1c2b1e-0a52-4f43-9d4f-2f2f6f1c2b1e/cancel")]
    [InlineData("POST", "/api/requests/6f1c2b1e-0a52-4f43-9d4f-2f2f6f1c2b1e/withdraw")]
    public async Task Browsing_claiming_cancelling_and_withdrawing_without_a_session_answer_401(string method, string path)
    {
        using var client = _factory.CreateSpaClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Quote_returns_the_calculators_cost_in_quarter_credits()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var quote = await client.GetFromJsonAsync<JsonElement>(
            "/api/requests/quote?durationMinutes=90&childCount=2&location=RequestingFamilysHouse&transport=None&lunch=true&snack=false&dinner=false");

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
