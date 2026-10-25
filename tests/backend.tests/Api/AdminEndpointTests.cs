using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;

namespace backend.tests.Api;

public class AdminEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Settle_needs_a_session()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.PostAsync("/api/admin/settle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Settle_is_forbidden_to_a_member_who_is_not_the_admin()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsync("/api/admin/settle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("not_admin");
    }

    [Fact]
    public async Task Settle_applies_expiry_and_payout_and_a_repeat_run_does_nothing()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var requester = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var sitter = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var adminClient = _factory.CreateMemberClient(admin);
        using var requesterClient = _factory.CreateMemberClient(requester);
        using var sitterClient = _factory.CreateMemberClient(sitter);
        var startsAt = _factory.Clock.GetUtcNow().AddHours(10);
        var body = new
        {
            startsAt,
            durationMinutes = 120,
            childCount = 1,
            location = "SittersHouse",
            transport = "None",
            lunch = false,
            snack = false,
            dinner = false,
            area = "Riverside",
            address = "12 River Rd",
        };
        var claimed = await (await requesterClient.PostAsJsonAsync("/api/requests", body)).Content.ReadFromJsonAsync<JsonElement>();
        (await sitterClient.PostAsync($"/api/requests/{claimed.GetProperty("id").GetString()}/claim", null)).EnsureSuccessStatusCode();
        (await requesterClient.PostAsJsonAsync("/api/requests", body)).EnsureSuccessStatusCode();
        _factory.Clock.Advance(TimeSpan.FromHours(11));

        var first = await adminClient.PostAsync("/api/admin/settle", null);
        var again = await adminClient.PostAsync("/api/admin/settle", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await first.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("expired").GetInt32().Should().Be(1);
        result.GetProperty("completed").GetInt32().Should().Be(1);
        result.GetProperty("failed").GetInt32().Should().Be(0);
        var repeat = await again.Content.ReadFromJsonAsync<JsonElement>();
        repeat.GetProperty("expired").GetInt32().Should().Be(0);
        repeat.GetProperty("completed").GetInt32().Should().Be(0);
        var requesterAccount = await requesterClient.GetFromJsonAsync<JsonElement>("/api/me/account");
        requesterAccount.GetProperty("availableQuarterCredits").GetInt64().Should().Be(32);
        requesterAccount.GetProperty("escrowedQuarterCredits").GetInt64().Should().Be(0);
        var sitterAccount = await sitterClient.GetFromJsonAsync<JsonElement>("/api/me/account");
        sitterAccount.GetProperty("availableQuarterCredits").GetInt64().Should().Be(48);
    }

    [Fact]
    public async Task Offboard_needs_a_session()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.PostAsync($"/api/admin/members/{Guid.NewGuid()}/offboard", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Offboard_is_forbidden_to_a_member_who_is_not_the_admin()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var victim = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.PostAsync($"/api/admin/members/{victim}/offboard", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Offboard_closes_the_member_burns_their_balance_and_locks_them_out()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var leaver = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var adminClient = _factory.CreateMemberClient(admin);
        using var leaverClient = _factory.CreateMemberClient(leaver);
        var body = new
        {
            startsAt = _factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 120,
            childCount = 1,
            location = "SittersHouse",
            transport = "None",
            lunch = false,
            snack = false,
            dinner = false,
            area = "Riverside",
            address = "12 River Rd",
        };
        (await leaverClient.PostAsJsonAsync("/api/requests", body)).EnsureSuccessStatusCode();

        var response = await adminClient.PostAsync($"/api/admin/members/{leaver}/offboard", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("cancelled").GetInt32().Should().Be(1);
        result.GetProperty("withdrawn").GetInt32().Should().Be(0);
        result.GetProperty("burnedQuarterCredits").GetInt64().Should().Be(40);
        (await leaverClient.GetAsync("/api/me/account")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Offboard_refuses_the_admin()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        using var adminClient = _factory.CreateMemberClient(admin);

        var response = await adminClient.PostAsync($"/api/admin/members/{admin}/offboard", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("cannot_offboard_admin");
        (await adminClient.GetAsync("/api/me/account")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Offboard_of_an_unknown_member_is_not_found()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        using var adminClient = _factory.CreateMemberClient(admin);

        var response = await adminClient.PostAsync($"/api/admin/members/{Guid.NewGuid()}/offboard", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("member_not_found");
    }

    [Fact]
    public async Task Erase_is_forbidden_to_a_member_who_is_not_the_admin()
    {
        var member = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(member);

        var response = await client.PostAsync($"/api/admin/members/{member}/erase", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Erase_refuses_an_open_member()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var member = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var adminClient = _factory.CreateMemberClient(admin);

        var response = await adminClient.PostAsync($"/api/admin/members/{member}/erase", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("member_not_closed");
    }

    [Fact]
    public async Task Erase_of_an_offboarded_member_succeeds_with_no_content()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var leaver = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var adminClient = _factory.CreateMemberClient(admin);
        (await adminClient.PostAsync($"/api/admin/members/{leaver}/offboard", null)).EnsureSuccessStatusCode();

        var response = await adminClient.PostAsync($"/api/admin/members/{leaver}/erase", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Erase_of_an_unknown_member_is_not_found()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        using var adminClient = _factory.CreateMemberClient(admin);

        var response = await adminClient.PostAsync($"/api/admin/members/{Guid.NewGuid()}/erase", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
