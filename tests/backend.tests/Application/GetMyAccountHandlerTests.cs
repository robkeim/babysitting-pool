using AwesomeAssertions;
using BabysittingPool.Application.Credits;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class GetMyAccountHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Returns_balances_and_ledger_entries_as_quarter_credits_newest_first()
    {
        var memberId = await _database.SeedAccountAsync(
            Credits.FromDecimal(10.25m), Credits.FromDecimal(2m),
            Credits.FromDecimal(10m), Credits.FromDecimal(0.25m));

        var account = await HandleAsync(new GetMyAccountQuery(memberId));

        account.Should().NotBeNull();
        account!.AvailableQuarterCredits.Should().Be(41);
        account.EscrowedQuarterCredits.Should().Be(8);
        account.Ledger.Items.Select(e => e.AmountQuarterCredits).Should().Equal(1, 40);
        account.Ledger.Items.Should().OnlyContain(e => e.Kind == "Grant");
        account.Ledger.Items[0].OccurredAt.Should().Be(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        account.Ledger.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Pages_through_the_ledger_with_a_cursor()
    {
        var memberId = await _database.SeedAccountAsync(
            Credits.FromDecimal(3m), Credits.FromQuarterCredits(0),
            Credits.FromDecimal(1m), Credits.FromDecimal(1m), Credits.FromDecimal(1m));
        var first = await HandleAsync(new GetMyAccountQuery(memberId, Limit: 2));

        first!.Ledger.Items.Should().HaveCount(2);
        first.Ledger.NextCursor.Should().NotBeNullOrEmpty();

        var second = await HandleAsync(new GetMyAccountQuery(memberId, first.Ledger.NextCursor, Limit: 2));

        second!.Ledger.Items.Should().HaveCount(1);
        second.Ledger.NextCursor.Should().BeNull();
        first.Ledger.Items.Concat(second.Ledger.Items).Select(e => e.Id).Should().OnlyHaveUniqueItems();
        first.Ledger.Items.Concat(second.Ledger.Items).Select(e => e.OccurredAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Paging_never_skips_or_repeats_entries_with_the_same_timestamp()
    {
        var memberId = await _database.SeedSimultaneousGrantsAsync(5);
        var seen = new List<Guid>();
        string? cursor = null;

        do
        {
            var page = (await HandleAsync(new GetMyAccountQuery(memberId, cursor, Limit: 2)))!.Ledger;
            seen.AddRange(page.Items.Select(e => e.Id));
            cursor = page.NextCursor;
        } while (cursor is not null);

        seen.Should().HaveCount(5).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Returns_nothing_for_a_member_without_an_account()
    {
        var account = await HandleAsync(new GetMyAccountQuery(Guid.NewGuid()));

        account.Should().BeNull();
    }

    private async Task<AccountView?> HandleAsync(GetMyAccountQuery query)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GetMyAccountHandler>().HandleAsync(query);
    }
}
