using AwesomeAssertions;
using BabysittingPool.Application;
using BabysittingPool.Application.Admin;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Application;

public class AdminViewsHandlerTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private async Task<Guid> AddMemberAsync(
        string name, DateTime createdAt, decimal available = 10m, bool isAdmin = false, DateTime? closedAt = null, decimal escrowed = 0m)
    {
        var id = Guid.NewGuid();
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        db.Members.Add(new MemberRecord
        {
            Id = id,
            Email = $"{name}@example.test",
            DisplayName = name,
            IsAdmin = isAdmin,
            CreatedAt = createdAt,
            ClosedAt = closedAt,
        });
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = id,
            Available = Credits.FromDecimal(available),
            Escrowed = Credits.FromDecimal(escrowed),
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task AddRequestAsync(
        Guid requester, Guid? sitter, RequestStatus status, DateTime startsAt, DateTime? claimCutoffAt = null)
    {
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        db.Requests.Add(new RequestRecord
        {
            Id = Guid.NewGuid(),
            RequesterId = requester,
            SitterId = sitter,
            StartsAt = startsAt,
            DurationMinutes = 120,
            ChildCount = 1,
            Area = "Riverside",
            Address = "12 River Rd",
            Cost = Credits.FromDecimal(2m),
            ClaimCutoffAt = claimCutoffAt ?? startsAt.AddHours(-3),
            PostedAt = startsAt.AddDays(-1),
            Status = status,
        });
        await db.SaveChangesAsync();
    }

    private async Task AddLedgerAsync(Guid memberId, LedgerEntryKind kind, decimal amount, DateTime at)
    {
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = kind,
            Amount = Credits.FromDecimal(amount),
            OccurredAt = at,
        });
        await db.SaveChangesAsync();
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _database.CreateScope();
        return await action(scope.ServiceProvider);
    }

    [Fact]
    public async Task Dormant_list_shows_members_past_the_threshold_longest_dormant_first()
    {
        var longest = await AddMemberAsync("longest", Today.AddDays(-100), available: 4m);
        var shorter = await AddMemberAsync("shorter", Today.AddDays(-50));
        await AddMemberAsync("fresh", Today.AddDays(-10));

        var dormant = await RunAsync(sp => sp.GetRequiredService<ListDormantMembersHandler>().HandleAsync(default));

        dormant.Select(d => d.Id).Should().Equal(longest, shorter);
        dormant[0].Should().Be(new DormantMemberView(longest, "longest", "longest@example.test", Today.AddDays(-100), 16));
    }

    [Fact]
    public async Task Last_activity_is_the_last_completed_request_in_either_role_else_account_creation()
    {
        var asSitter = await AddMemberAsync("sitter", Today.AddDays(-100));
        var asRequester = await AddMemberAsync("requester", Today.AddDays(-100));
        var other = await AddMemberAsync("other", Today.AddDays(-100));
        await AddRequestAsync(other, asSitter, RequestStatus.Completed, Today.AddDays(-5));
        await AddRequestAsync(asRequester, other, RequestStatus.Completed, Today.AddDays(-60));
        await AddRequestAsync(asRequester, null, RequestStatus.Canceled, Today.AddDays(-2));

        var dormant = await RunAsync(sp => sp.GetRequiredService<ListDormantMembersHandler>().HandleAsync(default));

        // The sitter and "other" were active 5 days ago; only the Requester of the 60-day-old Request is dormant.
        dormant.Select(d => (d.Id, d.LastActivityAt)).Should().Equal((asRequester, Today.AddDays(-60)));
        dormant.Should().NotContain(d => d.Id == asSitter);
    }

    [Fact]
    public async Task Dormant_list_leaves_out_the_admin_and_closed_members()
    {
        await AddMemberAsync("admin", Today.AddDays(-100), isAdmin: true);
        await AddMemberAsync("gone", Today.AddDays(-100), available: 0m, closedAt: Today.AddDays(-20));
        var stays = await AddMemberAsync("stays", Today.AddDays(-100));

        var dormant = await RunAsync(sp => sp.GetRequiredService<ListDormantMembersHandler>().HandleAsync(default));

        dormant.Select(d => d.Id).Should().Equal(stays);
    }

    [Fact]
    public async Task Pool_overview_totals_credits_burns_and_requests()
    {
        var a = await AddMemberAsync("a", Today.AddDays(-5), available: 6m, escrowed: 2m);
        var b = await AddMemberAsync("b", Today.AddDays(-5), available: 4m);
        await AddMemberAsync("closed", Today.AddDays(-5), available: 0m, closedAt: Today.AddDays(-1));
        await AddLedgerAsync(a, LedgerEntryKind.Demurrage, 2m, Today.AddDays(-3));
        await AddLedgerAsync(b, LedgerEntryKind.Demurrage, 1.25m, Today.AddDays(-3));
        await AddLedgerAsync(b, LedgerEntryKind.Burn, 7m, Today.AddDays(-1));
        await AddLedgerAsync(b, LedgerEntryKind.Grant, 10m, Today.AddDays(-5));
        await AddRequestAsync(a, null, RequestStatus.Posted, Today.AddDays(2));
        await AddRequestAsync(a, null, RequestStatus.Posted, Today.AddHours(1), claimCutoffAt: Today.AddHours(-1));
        await AddRequestAsync(a, b, RequestStatus.Claimed, Today.AddDays(1));
        await AddRequestAsync(a, b, RequestStatus.Claimed, Today.AddHours(-2));
        await AddRequestAsync(a, b, RequestStatus.Completed, Today.AddDays(-3));
        await AddRequestAsync(a, null, RequestStatus.Canceled, Today.AddDays(3));
        await AddRequestAsync(a, null, RequestStatus.Expired, Today.AddDays(-3));

        var overview = await RunAsync(sp => sp.GetRequiredService<GetPoolOverviewHandler>().HandleAsync(default));

        overview.Should().Be(new PoolOverview(
            CreditsInCirculationQuarterCredits: 48,
            EscrowedQuarterCredits: 8,
            BurnedByDemurrageQuarterCredits: 13,
            BurnedByOffboardingQuarterCredits: 28,
            OpenMembers: 2,
            Requests: new RequestCounts(Posted: 1, Claimed: 1, Completed: 2, Canceled: 1, Expired: 2)));
    }

    [Fact]
    public async Task Member_detail_shows_balances_and_ledger_newest_first_and_is_null_for_a_stranger()
    {
        var member = await AddMemberAsync("m", Today.AddDays(-5), available: 8m, escrowed: 1m);
        await AddLedgerAsync(member, LedgerEntryKind.Grant, 10m, Today.AddDays(-5));
        await AddLedgerAsync(member, LedgerEntryKind.Escrow, 1m, Today.AddDays(-2));

        var detail = await RunAsync(sp => sp.GetRequiredService<GetMemberDetailHandler>().HandleAsync(member, null, null, default));
        var stranger = await RunAsync(sp => sp.GetRequiredService<GetMemberDetailHandler>().HandleAsync(Guid.NewGuid(), null, null, default));

        detail!.Member.Should().Match<MemberSummaryView>(m => m.AvailableQuarterCredits == 32 && m.EscrowedQuarterCredits == 4);
        detail.Ledger.Items.Select(e => e.Kind).Should().Equal(LedgerEntryKind.Escrow, LedgerEntryKind.Grant);
        stranger.Should().BeNull();
    }

    [Fact]
    public async Task Member_requests_show_both_roles_with_the_status_as_of_now_and_no_address()
    {
        var member = await AddMemberAsync("m", Today.AddDays(-5));
        var other = await AddMemberAsync("o", Today.AddDays(-5));
        await AddRequestAsync(member, null, RequestStatus.Posted, Today.AddDays(2));
        await AddRequestAsync(other, member, RequestStatus.Claimed, Today.AddHours(-1));
        await AddRequestAsync(other, null, RequestStatus.Posted, Today.AddDays(3));

        var requests = await RunAsync(sp => sp.GetRequiredService<ListMemberRequestsHandler>().HandleAsync(member, null, null, default));
        var stranger = await RunAsync(sp => sp.GetRequiredService<ListMemberRequestsHandler>().HandleAsync(Guid.NewGuid(), null, null, default));

        requests!.Items.Select(r => (r.Role, r.Status)).Should().Equal(
            (MemberRequestRole.Requester, RequestStatus.Posted), (MemberRequestRole.Sitter, RequestStatus.Completed));
        stranger.Should().BeNull();
    }
}
