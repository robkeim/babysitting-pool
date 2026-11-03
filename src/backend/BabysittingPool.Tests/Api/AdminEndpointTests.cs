using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Tests.Support;

namespace BabysittingPool.Tests.Api;

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
    public async Task Listing_members_needs_a_session()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.GetAsync("/api/admin/members");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listing_members_is_forbidden_to_a_member_who_is_not_the_admin()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        var response = await client.GetAsync("/api/admin/members");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Listing_members_shows_open_and_closed_ones_and_who_is_erased()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var open = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var closed = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var erased = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var adminClient = _factory.CreateMemberClient(admin);
        (await adminClient.PostAsync($"/api/admin/members/{closed}/offboard", null)).EnsureSuccessStatusCode();
        (await adminClient.PostAsync($"/api/admin/members/{erased}/offboard", null)).EnsureSuccessStatusCode();
        (await adminClient.PostAsync($"/api/admin/members/{erased}/erase", null)).EnsureSuccessStatusCode();

        var response = await adminClient.GetAsync("/api/admin/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        var items = list.ToDictionary(m => m.GetProperty("id").GetGuid());
        items[admin].GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        items[open].GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.Null);
        items[open].GetProperty("isErased").GetBoolean().Should().BeFalse();
        items[closed].GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.String);
        items[closed].GetProperty("isErased").GetBoolean().Should().BeFalse();
        items[erased].GetProperty("isErased").GetBoolean().Should().BeTrue();
        items[erased].GetProperty("email").GetString().Should().Be("Former member");
        var order = list.Select(m => m.GetProperty("closedAt").ValueKind == JsonValueKind.Null).ToList();
        order.Should().BeInDescendingOrder("open Members come before closed ones");
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

    [Fact]
    public async Task Invitations_are_forbidden_to_a_member_who_is_not_the_admin()
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        (await client.GetAsync("/api/admin/invitations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/admin/invitations", new { email = "ada@example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/admin/invitations/{Guid.NewGuid()}/resend", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_admin_invites_lists_and_resends()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        using var client = _factory.CreateMemberClient(admin);

        var invited = await client.PostAsJsonAsync("/api/admin/invitations", new { email = " Ada@Example.com " });

        invited.StatusCode.Should().Be(HttpStatusCode.Created);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        invitation.GetProperty("email").GetString().Should().Be("ada@example.com");
        invitation.GetProperty("status").GetString().Should().Be("Pending");
        var id = invitation.GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/admin/invitations");
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(id);

        var resent = await client.PostAsync($"/api/admin/invitations/{id}/resend", null);

        resent.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inviting_rejects_a_bad_address_a_duplicate_and_resending_an_unknown_invitation()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        using var client = _factory.CreateMemberClient(admin);

        (await client.PostAsJsonAsync("/api/admin/invitations", new { email = "nope" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/admin/invitations", new { email = "ada@example.com" })).EnsureSuccessStatusCode();
        var duplicate = await client.PostAsJsonAsync("/api/admin/invitations", new { email = "ada@example.com" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("invitation_already_sent");
        (await client.PostAsync($"/api/admin/invitations/{Guid.NewGuid()}/resend", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/admin/members")]
    [InlineData("/api/admin/pool-overview")]
    [InlineData("/api/admin/dormant-members")]
    public async Task Member_and_pool_views_are_forbidden_to_a_member_who_is_not_the_admin(string route)
    {
        var memberId = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        using var client = _factory.CreateMemberClient(memberId);

        (await client.GetAsync(new Uri(route, UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_admin_reads_members_detail_requests_overview_and_dormant_list()
    {
        var admin = await _factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await _factory.MakeAdminAsync(admin);
        var member = await _factory.SeedAccountAsync(Credits.FromDecimal(5m), Credits.FromQuarterCredits(0), Credits.FromDecimal(5m));
        using var client = _factory.CreateMemberClient(admin);

        var members = await client.GetFromJsonAsync<JsonElement>("/api/admin/members");
        members.GetProperty("items").GetArrayLength().Should().Be(2);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members/{member}");
        detail.GetProperty("member").GetProperty("availableQuarterCredits").GetInt64().Should().Be(20);
        detail.GetProperty("ledger").GetProperty("items").GetArrayLength().Should().Be(1);
        (await client.GetFromJsonAsync<JsonElement>($"/api/admin/members/{member}/requests"))
            .GetProperty("items").GetArrayLength().Should().Be(0);
        (await client.GetFromJsonAsync<JsonElement>("/api/admin/pool-overview"))
            .GetProperty("openMembers").GetInt32().Should().Be(2);
        (await client.GetFromJsonAsync<JsonElement>("/api/admin/dormant-members"))
            .GetProperty("items").GetArrayLength().Should().Be(0);
        (await client.GetAsync($"/api/admin/members/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/admin/members/{member}?cursor=garbage")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
