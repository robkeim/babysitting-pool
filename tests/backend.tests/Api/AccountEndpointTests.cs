using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Application.Addresses;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.tests.Api;

public class AccountEndpointTests : IDisposable
{
    private static readonly AddressSuggestion Amiens = new(
        "80021_6115_12", "12 Rue de la Paix 80000 Amiens", "Amiens", "80021", "80", "80000", "housenumber");

    private readonly FakeSuggester _suggester = new([Amiens]);
    private readonly ApiFactory _factory;

    public AccountEndpointTests() =>
        _factory = new ApiFactory(configureServices: services => services.Replace(ServiceDescriptor.Singleton<IAddressSuggester>(_suggester)));

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
    public async Task Rejects_a_request_without_a_session_with_401_problem_json()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me/account");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Answers_404_problem_json_when_the_member_has_no_account()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

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

    [Fact]
    public async Task The_profile_is_set_from_a_BAN_pick_and_the_area_is_derived()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/me/profile");
        var response = await client.PutAsJsonAsync(
            "/api/me/profile", new { banId = Amiens.BanId, label = Amiens.Label, instructions = " apt 3, ring twice " });
        var after = await client.GetFromJsonAsync<JsonElement>("/api/me/profile");

        before.GetProperty("area").GetString().Should().Be("Riverside (80)");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        after.GetProperty("area").GetString().Should().Be("Amiens (80)");
        after.GetProperty("address").GetString().Should().Be(Amiens.Label);
        after.GetProperty("commune").GetString().Should().Be("Amiens");
        after.GetProperty("inseeCode").GetString().Should().Be("80021");
        after.GetProperty("departmentNumber").GetString().Should().Be("80");
        after.GetProperty("postcode").GetString().Should().Be("80000");
        after.GetProperty("instructions").GetString().Should().Be("apt 3, ring twice");
        _suggester.Queries.Should().Equal(Amiens.Label);
    }

    [Fact]
    public async Task Instructions_can_change_without_picking_again_and_leave_the_address_alone()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PutAsJsonAsync("/api/me/profile", new { instructions = "back door" });
        var after = await client.GetFromJsonAsync<JsonElement>("/api/me/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        after.GetProperty("area").GetString().Should().Be("Riverside (80)");
        after.GetProperty("address").GetString().Should().Be("12 River Rd");
        after.GetProperty("instructions").GetString().Should().Be("back door");
        _suggester.Queries.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Member_with_no_address_cannot_save_without_a_pick()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);
        await client.PutAsJsonAsync("/api/me/profile", new { banId = Amiens.BanId, label = Amiens.Label });
        await EraseHomeAsync(memberId);

        var response = await client.PutAsJsonAsync("/api/me/profile", new { instructions = "back door" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_address");
    }

    [Fact]
    public async Task A_forged_label_for_a_real_id_cannot_change_what_is_stored()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        // The client names Amiens' id but a label whose BAN results hold a different id.
        var response = await client.PutAsJsonAsync("/api/me/profile", new { banId = Amiens.BanId, label = "1 Rue Inconnue 75001 Paris" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_address");
        (await client.GetFromJsonAsync<JsonElement>("/api/me/profile")).GetProperty("area").GetString().Should().Be("Riverside (80)");
    }

    [Theory]
    [InlineData("nope", "12 Rue de la Paix 80000 Amiens")]
    [InlineData("", "12 Rue de la Paix 80000 Amiens")]
    [InlineData("80021_6115_12", "")]
    [InlineData("80021_6115_12", "ab")]
    public async Task An_unknown_id_or_a_missing_pick_is_refused_with_422_and_leaves_the_profile_alone(string banId, string label)
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PutAsJsonAsync("/api/me/profile", new { banId, label });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_address");
        (await client.GetFromJsonAsync<JsonElement>("/api/me/profile")).GetProperty("area").GetString().Should().Be("Riverside (80)");
    }

    [Fact]
    public async Task Instructions_over_300_characters_are_refused_with_422()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PutAsJsonAsync(
            "/api/me/profile", new { banId = Amiens.BanId, label = Amiens.Label, instructions = new string('x', 301) });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_instructions");
        (await client.GetFromJsonAsync<JsonElement>("/api/me/profile")).GetProperty("area").GetString().Should().Be("Riverside (80)");
    }

    [Fact]
    public async Task When_BAN_is_down_the_save_is_503_and_nothing_changes()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);
        _suggester.Fail = true;

        var response = await client.PutAsJsonAsync("/api/me/profile", new { banId = Amiens.BanId, label = Amiens.Label });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("address_service_unavailable");
        (await client.GetFromJsonAsync<JsonElement>("/api/me/profile")).GetProperty("area").GetString().Should().Be("Riverside (80)");
    }

    [Fact]
    public async Task The_profile_needs_a_session()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync("/api/me/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task EraseHomeAsync(Guid memberId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        await db.Members.Where(m => m.Id == memberId).ExecuteUpdateAsync(m => m.SetProperty(x => x.Commune, (string?)null));
    }

    private sealed class FakeSuggester(params AddressSuggestion[] known) : IAddressSuggester
    {
        public List<string> Queries { get; } = [];

        public bool Fail { get; set; }

        /// <summary>Like BAN: finds a suggestion only when the query is its label.</summary>
        public Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(string query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Fail
                ? throw new AddressServiceUnavailableException()
                : Task.FromResult<IReadOnlyList<AddressSuggestion>>(known.Where(k => k.Label == query).ToList());
        }
    }
}
