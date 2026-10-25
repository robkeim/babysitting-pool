using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Application.Flags;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace backend.tests.Api;

public class FeatureFlagTests : IDisposable
{
    private static readonly FeatureFlag Alpha = new("alpha", false, 1);
    private static readonly FeatureFlag Beta = new("beta", false, 2);
    private static readonly FeatureFlag Seeded = new("seeded", false, 3);

    private readonly ApiFactory _factory = new(
        flags: [Alpha, Beta, Seeded], features: new Dictionary<string, string> { ["seeded"] = "true" });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Two_flags_at_ten_percent_pick_different_cohorts()
    {
        await SaveAsync(Alpha, Settings(rollout: 10));
        await SaveAsync(Beta, Settings(rollout: 10));
        var ids = Enumerable.Range(1, 1000).Select(i => new Guid(i, 0, 0, new byte[8])).ToList();

        var alpha = await CohortAsync(Alpha, ids);
        var beta = await CohortAsync(Beta, ids);

        alpha.Count.Should().BeInRange(50, 150);
        beta.Count.Should().BeInRange(50, 150);
        alpha.Should().NotBeEquivalentTo(beta);
    }

    [Fact]
    public async Task Deny_beats_allow_and_allow_beats_the_percentage()
    {
        var allowed = Guid.NewGuid();
        var both = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var ids = new[] { allowed, both, outsider };
        await SaveAsync(Alpha, Settings(rollout: 0, allow: [allowed, both], deny: [both]));

        (await CohortAsync(Alpha, ids)).Should().BeEquivalentTo([allowed], "rollout 0 admits only the allow list, and deny beats allow");

        await SaveAsync(Alpha, Settings(rollout: 100, deny: [both]));

        (await CohortAsync(Alpha, ids)).Should().BeEquivalentTo([allowed, outsider], "deny beats a 100% rollout");
    }

    [Fact]
    public async Task A_role_group_has_its_own_rollout()
    {
        await SaveAsync(Alpha, Settings(rollout: 0, roles: [new RoleRollout("Admin", 100)]));
        var id = Guid.NewGuid();

        (await IsOnAsync(Alpha, id, "Admin")).Should().BeTrue();
        (await IsOnAsync(Alpha, id, "Member")).Should().BeFalse();
    }

    [Fact]
    public async Task A_disabled_flag_is_off_for_everyone_including_the_allow_list()
    {
        var id = Guid.NewGuid();
        await SaveAsync(Alpha, Settings(enabled: false, rollout: 100, allow: [id]));

        (await IsOnAsync(Alpha, id, "Member")).Should().BeFalse();
    }

    [Fact]
    public async Task Configuration_seeds_the_first_value_only()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>().GetAllAsync(CancellationToken.None);

        stored["seeded"].Enabled.Should().BeTrue("Features:seeded=true");
        stored["alpha"].Enabled.Should().BeFalse("the registry default");
    }

    [Fact]
    public async Task A_gated_endpoint_is_404_while_its_flag_is_off_and_changes_take_effect_without_a_restart()
    {
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        using var client = _factory.CreateMemberClient(admin);

        var off = await client.GetAsync("/api/test/gated/alpha");
        var put = await client.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", enabled: true));
        var on = await client.GetAsync("/api/test/gated/alpha");
        await client.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", enabled: false));
        var offAgain = await client.GetAsync("/api/test/gated/alpha");

        off.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("feature_off");
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        on.StatusCode.Should().Be(HttpStatusCode.OK);
        offAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_deny_list_turns_a_member_away_over_http()
    {
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        var member = await _factory.SeedMemberAsync();
        using var adminClient = _factory.CreateMemberClient(admin);
        using var memberClient = _factory.CreateMemberClient(member);

        await adminClient.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", enabled: true, deny: [member]));

        (await adminClient.GetAsync("/api/test/gated/alpha")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await memberClient.GetAsync("/api/test/gated/alpha")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_shows_each_declared_flag_with_its_removal_issue()
    {
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        using var client = _factory.CreateMemberClient(admin);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/feature-flags");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("name").GetString()).Should().Equal("alpha", "beta", "seeded");
        items[0].GetProperty("removalIssue").GetInt32().Should().Be(1);
        items[2].GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_admin_api_is_for_the_Admin_only_and_rejects_bad_input()
    {
        var member = await _factory.SeedMemberAsync();
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        using var memberClient = _factory.CreateMemberClient(member);
        using var adminClient = _factory.CreateMemberClient(admin);

        (await memberClient.GetAsync("/api/admin/feature-flags")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await memberClient.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", true))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await adminClient.PutAsJsonAsync("/api/admin/feature-flags", Body("nope", true))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await adminClient.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", true, rollout: 101))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_caller_gets_their_flags_resolved_and_an_anonymous_caller_only_the_fully_on_ones()
    {
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        var member = await _factory.SeedMemberAsync();
        using var adminClient = _factory.CreateMemberClient(admin);
        using var memberClient = _factory.CreateMemberClient(member);
        using var anonymous = _factory.CreateSpaClient();
        await adminClient.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", enabled: true, deny: [member]));
        await adminClient.PutAsJsonAsync("/api/admin/feature-flags", Body("beta", enabled: true, rollout: 50));

        var forAdmin = await adminClient.GetFromJsonAsync<Dictionary<string, bool>>("/api/feature-flags");
        var forMember = await memberClient.GetFromJsonAsync<Dictionary<string, bool>>("/api/feature-flags");
        var forAnonymous = await anonymous.GetFromJsonAsync<Dictionary<string, bool>>("/api/feature-flags");

        forAdmin!["alpha"].Should().BeTrue();
        forMember!["alpha"].Should().BeFalse("the deny list");
        forMember.Keys.Should().Equal("alpha", "beta", "seeded");
        forAnonymous.Should().Equal(new Dictionary<string, bool> { ["alpha"] = true, ["beta"] = false, ["seeded"] = true });
    }

    [Fact]
    public async Task The_flags_answer_carries_an_ETag_that_turns_a_repeat_into_304_until_a_flag_changes()
    {
        var admin = await _factory.SeedMemberAsync();
        await _factory.MakeAdminAsync(admin);
        using var client = _factory.CreateMemberClient(admin);

        var first = await client.GetAsync("/api/feature-flags");
        var etag = first.Headers.ETag!.ToString();
        var repeat = await Conditional(client, etag);
        await client.PutAsJsonAsync("/api/admin/feature-flags", Body("alpha", enabled: true));
        var afterChange = await Conditional(client, etag);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeat.StatusCode.Should().Be(HttpStatusCode.NotModified);
        afterChange.StatusCode.Should().Be(HttpStatusCode.OK);
        afterChange.Headers.ETag!.ToString().Should().NotBe(etag);
    }

    private static async Task<HttpResponseMessage> Conditional(HttpClient client, string etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/feature-flags");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return await client.SendAsync(request);
    }

    [Fact]
    public void A_configured_flag_that_is_not_declared_stops_the_API_starting()
    {
        using var broken = new ApiFactory(flags: [Alpha], features: new Dictionary<string, string> { ["typo"] = "true" });

        var start = () => broken.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*Features:typo*");
    }

    private static FeatureFlagSettings Settings(
        bool enabled = true, int rollout = 100, Guid[]? allow = null, Guid[]? deny = null, RoleRollout[]? roles = null) =>
        new(enabled, rollout, allow ?? [], deny ?? [], roles ?? []);

    private static object Body(
        string name, bool enabled, int rollout = 100, Guid[]? deny = null) =>
        new { name, enabled, rolloutPercent = rollout, allowedMemberIds = Array.Empty<Guid>(), deniedMemberIds = deny ?? [], roleRollouts = Array.Empty<object>() };

    private async Task SaveAsync(FeatureFlag flag, FeatureFlagSettings settings)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>().SaveAsync(flag.Name, settings, CancellationToken.None);
    }

    private async Task<List<Guid>> CohortAsync(FeatureFlag flag, IEnumerable<Guid> ids)
    {
        var cohort = new List<Guid>();
        foreach (var id in ids)
        {
            if (await IsOnAsync(flag, id, "Member"))
            {
                cohort.Add(id);
            }
        }

        return cohort;
    }

    private async Task<bool> IsOnAsync(FeatureFlag flag, Guid memberId, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IFeatureManager>();
        return await manager.IsEnabledAsync(flag.Name, new TargetingContext { UserId = memberId.ToString(), Groups = [role] });
    }
}
