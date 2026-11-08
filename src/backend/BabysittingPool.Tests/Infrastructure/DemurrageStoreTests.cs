using AwesomeAssertions;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Infrastructure;

public class DemurrageStoreTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    [Fact]
    public async Task A_Member_without_a_Credit_Account_has_no_Demurrage_history()
    {
        var member = await AccountSeeder.SeedMemberAsync(_database.Services, _database.Clock);
        await using var scope = _database.CreateScope();

        var candidate = await scope.ServiceProvider.GetRequiredService<IDemurrageStore>().FindAsync(member, default);

        candidate.Should().BeNull();
    }

    [Fact]
    public async Task A_stale_account_version_is_a_concurrency_conflict_and_nothing_is_charged()
    {
        var member = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var boundary = Now.AddDays(1);
        await using var first = _database.CreateScope();
        await using var second = _database.CreateScope();
        var firstStore = first.ServiceProvider.GetRequiredService<IDemurrageStore>();
        var secondStore = second.ServiceProvider.GetRequiredService<IDemurrageStore>();
        var firstRead = (await firstStore.FindAsync(member, default))!;
        var secondRead = (await secondStore.FindAsync(member, default))!;
        firstRead.Account.BurnAll();
        secondRead.Account.BurnAll();
        await firstStore.SaveChargedAsync(member, firstRead.Account, [new DemurrageCharge(boundary, 10m)], Now, default);

        var act = () => secondStore.SaveChargedAsync(member, secondRead.Account, [new DemurrageCharge(boundary, 10m)], Now, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        (await db.LedgerEntries.AsNoTracking().CountAsync(e => e.Kind == LedgerEntryKind.Demurrage)).Should().Be(1);
    }
}
