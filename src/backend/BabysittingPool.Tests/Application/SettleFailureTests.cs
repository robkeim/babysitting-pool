using AwesomeAssertions;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Application;

/// <summary>
/// Settle when one item fails (ADR 0007): the failure is counted, that item is left untouched, every other item is
/// still processed, and the next run retries it. A lost race changes the row the handler read, for real, just before
/// the save; an unexpected failure throws from the store.
/// </summary>
public class SettleFailureTests : IAsyncLifetime
{
    public enum Fault
    {
        LostRace,
        Unexpected,
    }

    private TestDatabase _database = null!;
    private StoreFaults _faults = null!;
    private Guid _requester;
    private Guid _sitter;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync(services =>
        {
            StoreFaults.Intercept<IRequestStore>(services);
            StoreFaults.Intercept<IDemurrageStore>(services);
            StoreFaults.Intercept<INotificationStore>(services);
        });
        _faults = _database.Services.GetRequiredService<StoreFaults>();
        _requester = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        _sitter = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<RequestView> PostAsync(int hoursAhead)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            _requester, Now.AddHours(hoursAhead), new CreditRequestDetails(120, 1));
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task ClaimAsync(Guid requestId)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(requestId, _sitter), default);
    }

    private async Task<SettleResult> SettleAsync()
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
    }

    private async Task<decimal[]> BalanceAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        var account = await _database.NewContext(scope).CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId);
        return [account.Available.Value, account.Escrowed.Value];
    }

    private async Task<RequestStatus> StatusAsync(Guid requestId)
    {
        await using var scope = _database.CreateScope();
        return (await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync(r => r.Id == requestId)).Status;
    }

    private async Task<int> EntriesAsync(LedgerEntryKind kind, Guid? requestId = null, Guid? memberId = null)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).LedgerEntries.AsNoTracking()
            .CountAsync(e => e.Kind == kind
                && (requestId == null || e.RequestId == requestId)
                && (memberId == null || e.MemberId == memberId));
    }

    private async Task BumpAccountVersionAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        await _database.NewContext(scope).CreditAccounts.Where(a => a.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Version, a => a.Version + 1));
    }

    private Func<object?[], Task> Failing(Fault fault, Guid account) => fault == Fault.LostRace
        ? _ => BumpAccountVersionAsync(account)
        : _ => throw new InvalidOperationException("The store failed.");

    [Theory]
    [InlineData(Fault.LostRace)]
    [InlineData(Fault.Unexpected)]
    public async Task A_failed_Expiry_leaves_that_request_untouched_and_the_next_run_retries_it(Fault fault)
    {
        var failing = await PostAsync(hoursAhead: 10);
        var healthy = await PostAsync(hoursAhead: 12);
        _database.Clock.Advance(TimeSpan.FromHours(10));
        _faults.Before(
            nameof(IRequestStore.SaveExpiredAsync),
            Failing(fault, _requester),
            args => ((Request)args[0]!).Id == failing.Id);

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(1, 0, 0, 1));
        (await StatusAsync(failing.Id)).Should().Be(RequestStatus.Posted);
        (await StatusAsync(healthy.Id)).Should().Be(RequestStatus.Expired);
        (await EntriesAsync(LedgerEntryKind.Refund, failing.Id)).Should().Be(0);
        (await EntriesAsync(LedgerEntryKind.Refund, healthy.Id)).Should().Be(1);
        (await BalanceAsync(_requester)).Should().Equal(8m, 2m);

        var retry = await SettleAsync();

        retry.Should().Be(new SettleResult(1, 0, 0, 0));
        (await StatusAsync(failing.Id)).Should().Be(RequestStatus.Expired);
        (await EntriesAsync(LedgerEntryKind.Refund, failing.Id)).Should().Be(1);
        (await BalanceAsync(_requester)).Should().Equal(10m, 0m);
    }

    [Theory]
    [InlineData(Fault.LostRace)]
    [InlineData(Fault.Unexpected)]
    public async Task A_failed_Payout_leaves_that_request_untouched_and_the_next_run_retries_it(Fault fault)
    {
        var failing = await PostAsync(hoursAhead: 10);
        var healthy = await PostAsync(hoursAhead: 14);
        await ClaimAsync(failing.Id);
        await ClaimAsync(healthy.Id);
        _database.Clock.Advance(TimeSpan.FromHours(15));
        _faults.Before(
            nameof(IRequestStore.SaveCompletedAsync),
            Failing(fault, _requester),
            args => ((Request)args[0]!).Id == failing.Id);

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 1, 0, 1));
        (await StatusAsync(failing.Id)).Should().Be(RequestStatus.Claimed);
        (await StatusAsync(healthy.Id)).Should().Be(RequestStatus.Completed);
        (await EntriesAsync(LedgerEntryKind.Payout, failing.Id)).Should().Be(0);
        (await EntriesAsync(LedgerEntryKind.Payout, healthy.Id)).Should().Be(1);
        (await BalanceAsync(_requester)).Should().Equal(6m, 2m);
        (await BalanceAsync(_sitter)).Should().Equal(12m, 0m);

        var retry = await SettleAsync();

        retry.Should().Be(new SettleResult(0, 1, 0, 0));
        (await StatusAsync(failing.Id)).Should().Be(RequestStatus.Completed);
        (await EntriesAsync(LedgerEntryKind.Payout, failing.Id)).Should().Be(1);
        (await BalanceAsync(_requester)).Should().Equal(6m, 0m);
        (await BalanceAsync(_sitter)).Should().Equal(14m, 0m);
    }

    [Theory]
    [InlineData(Fault.LostRace)]
    [InlineData(Fault.Unexpected)]
    public async Task A_failed_Demurrage_charge_leaves_that_Member_untouched_and_the_next_run_retries_it(Fault fault)
    {
        // The Members are inactive and the clock is at the first boundary past their 21 days.
        _database.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero));
        _faults.Before(
            nameof(IDemurrageStore.SaveChargedAsync),
            Failing(fault, _requester),
            args => (Guid)args[0]! == _requester);

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 0, 1, 1));
        (await BalanceAsync(_requester)).Should().Equal(10m, 0m);
        (await EntriesAsync(LedgerEntryKind.Demurrage, memberId: _requester)).Should().Be(0);
        (await BalanceAsync(_sitter)).Should().Equal(8m, 0m);
        (await EntriesAsync(LedgerEntryKind.Demurrage, memberId: _sitter)).Should().Be(1);

        var retry = await SettleAsync();

        retry.Should().Be(new SettleResult(0, 0, 1, 0));
        (await BalanceAsync(_requester)).Should().Equal(8m, 0m);
        (await EntriesAsync(LedgerEntryKind.Demurrage, memberId: _requester)).Should().Be(1);
        (await BalanceAsync(_sitter)).Should().Equal(8m, 0m);
    }

    [Fact]
    public async Task A_Member_who_disappears_between_listing_and_charging_is_skipped_and_the_others_are_charged()
    {
        _database.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero));
        _faults.Before(
            nameof(IDemurrageStore.FindAsync),
            async _ =>
            {
                await using var scope = _database.CreateScope();
                await _database.NewContext(scope).CreditAccounts.Where(a => a.MemberId == _requester).ExecuteDeleteAsync();
            },
            args => (Guid)args[0]! == _requester);

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 0, 1, 0));
        (await EntriesAsync(LedgerEntryKind.Demurrage, memberId: _requester)).Should().Be(0);
        (await BalanceAsync(_sitter)).Should().Equal(8m, 0m);
        (await EntriesAsync(LedgerEntryKind.Demurrage, memberId: _sitter)).Should().Be(1);
    }

    [Fact]
    public async Task A_failing_notification_sweep_does_not_stop_Settle_and_the_next_run_sweeps()
    {
        var posted = await PostAsync(hoursAhead: 10);
        await ClaimAsync(posted.Id);
        var notifications = await NotificationCountAsync();
        notifications.Should().BePositive();
        _database.Clock.Advance(TimeSpan.FromHours(11));
        _faults.Before(
            nameof(INotificationStore.DeleteForStartedRequestsAsync),
            _ => throw new InvalidOperationException("The store failed."));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 1, 0, 0));
        (await StatusAsync(posted.Id)).Should().Be(RequestStatus.Completed);
        (await BalanceAsync(_sitter)).Should().Equal(12m, 0m);
        (await NotificationCountAsync()).Should().Be(notifications);

        var retry = await SettleAsync();

        retry.Should().Be(new SettleResult(0, 0, 0, 0));
        (await NotificationCountAsync()).Should().Be(0);
    }

    private async Task<int> NotificationCountAsync()
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).Notifications.AsNoTracking().CountAsync();
    }
}
