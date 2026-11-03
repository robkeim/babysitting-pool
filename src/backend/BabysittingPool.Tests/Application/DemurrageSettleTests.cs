using AwesomeAssertions;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Application;

/// <summary>
/// Demurrage through Settle (spec §5). The clock starts Sunday 1 March 2026 09:00 UTC, when the Members are created;
/// boundaries are Mondays 00:00 UTC, so the first charge is on 23 March (21 days and 15 hours later).
/// </summary>
public class DemurrageSettleTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static DateTimeOffset Utc(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private async Task<Guid> SeedAsync(decimal available, decimal escrowed = 0m)
    {
        _database ??= await TestDatabase.CreateMigratedAsync();
        return await _database.SeedAccountAsync(Credits.FromDecimal(available), Credits.FromDecimal(escrowed));
    }

    private async Task<SettleResult> SettleAtAsync(DateTimeOffset at)
    {
        _database.Clock.SetUtcNow(at);
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
    }

    private async Task<CreditAccountRecord> AccountAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId);
    }

    private async Task<List<LedgerEntryRecord>> ChargesAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).LedgerEntries.AsNoTracking()
            .Where(e => e.MemberId == memberId && e.Kind == LedgerEntryKind.Demurrage)
            .OrderBy(e => e.Boundary).ToListAsync();
    }

    [Fact]
    public async Task An_inactive_Member_is_charged_at_the_first_boundary_past_21_days_and_a_repeat_run_does_nothing()
    {
        var member = await SeedAsync(10m);

        var before = await SettleAtAsync(Utc(3, 16));
        var first = await SettleAtAsync(Utc(3, 23));
        var again = await SettleAtAsync(Utc(3, 23, 5));

        before.Charged.Should().Be(0);
        first.Charged.Should().Be(1);
        again.Charged.Should().Be(0);
        (await AccountAsync(member)).Available.Value.Should().Be(8m);
        var charge = (await ChargesAsync(member)).Single();
        charge.Amount.Value.Should().Be(2m);
        charge.Boundary.Should().Be(Utc(3, 23).UtcDateTime);
        charge.RequestId.Should().BeNull();
    }

    [Fact]
    public async Task Demurrage_keeps_applying_weekly()
    {
        var member = await SeedAsync(10m);

        await SettleAtAsync(Utc(3, 23));
        await SettleAtAsync(Utc(3, 30));
        await SettleAtAsync(Utc(4, 6));

        (await AccountAsync(member)).Available.Value.Should().Be(4m);
        (await ChargesAsync(member)).Should().HaveCount(3);
    }

    [Fact]
    public async Task After_downtime_every_missed_boundary_is_applied()
    {
        var member = await SeedAsync(10m);

        var result = await SettleAtAsync(Utc(4, 7));

        result.Charged.Should().Be(1);
        (await AccountAsync(member)).Available.Value.Should().Be(4m);
        (await ChargesAsync(member)).Select(c => c.Boundary).Should().Equal(
            Utc(3, 23).UtcDateTime, Utc(3, 30).UtcDateTime, Utc(4, 6).UtcDateTime);
    }

    [Fact]
    public async Task The_charge_stops_at_zero_and_a_Member_with_nothing_left_is_not_charged_again()
    {
        var member = await SeedAsync(1m);

        await SettleAtAsync(Utc(3, 23));
        var later = await SettleAtAsync(Utc(4, 13));

        (await AccountAsync(member)).Available.Value.Should().Be(0m);
        var charge = (await ChargesAsync(member)).Single();
        charge.Amount.Value.Should().Be(1m);
        later.Should().Be(new SettleResult(0, 0, 0, 0));
    }

    [Fact]
    public async Task Escrowed_credits_are_not_touched()
    {
        var member = await SeedAsync(1m, escrowed: 3m);

        await SettleAtAsync(Utc(3, 23));
        await SettleAtAsync(Utc(3, 30));

        var account = await AccountAsync(member);
        account.Available.Value.Should().Be(0m);
        account.Escrowed.Value.Should().Be(3m);
    }

    [Fact]
    public async Task A_Completed_Request_as_Requester_or_Sitter_resets_the_window()
    {
        var requester = await SeedAsync(10m);
        var sitter = await SeedAsync(10m);
        var bystander = await SeedAsync(10m);
        _database.Clock.SetUtcNow(Utc(3, 10));
        await using (var scope = _database.CreateScope())
        {
            var posted = (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(
                new PostRequestCommand(requester, Utc(3, 10, 10).UtcDateTime, new CreditRequestDetails(120, 1)),
                default))!;
            await using var claimScope = _database.CreateScope();
            await claimScope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
                .HandleAsync(new ClaimRequestCommand(posted.Id, sitter), default);
        }

        await SettleAtAsync(Utc(3, 10, 11));
        var march30 = await SettleAtAsync(Utc(3, 30));
        var april6 = await SettleAtAsync(Utc(4, 6));

        // The Request started 10 March 10:00, so 30 March is 19 days on and 6 April is 26 days on.
        march30.Charged.Should().Be(1);
        april6.Charged.Should().Be(3);
        (await ChargesAsync(requester)).Select(c => c.Boundary).Should().Equal(Utc(4, 6).UtcDateTime);
        (await ChargesAsync(sitter)).Select(c => c.Boundary).Should().Equal(Utc(4, 6).UtcDateTime);
        (await ChargesAsync(bystander)).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_closed_Member_is_not_charged()
    {
        var member = await SeedAsync(10m);
        await using (var scope = _database.CreateScope())
        {
            var db = _database.NewContext(scope);
            (await db.Members.SingleAsync(m => m.Id == member)).ClosedAt = Utc(3, 2).UtcDateTime;
            await db.SaveChangesAsync();
        }

        var result = await SettleAtAsync(Utc(3, 30));

        result.Charged.Should().Be(0);
        (await AccountAsync(member)).Available.Value.Should().Be(10m);
    }

    [Fact]
    public async Task A_boundary_can_be_recorded_only_once_per_Member()
    {
        var member = await SeedAsync(10m);
        await SettleAtAsync(Utc(3, 23));
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = member,
            Kind = LedgerEntryKind.Demurrage,
            Amount = Credits.FromDecimal(2m),
            OccurredAt = Utc(3, 24).UtcDateTime,
            Boundary = Utc(3, 23).UtcDateTime,
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
