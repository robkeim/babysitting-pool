using AwesomeAssertions;
using BabysittingPool.Application;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class CancelAndWithdrawHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _requester;
    private Guid _sitter;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _requester = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        _sitter = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<RequestView> PostAsync()
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            _requester, Now.AddHours(10), new CreditRequestDetails(120, 1), "Riverside", "12 River Rd");
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task<RequestView> ClaimedAsync()
    {
        var posted = await PostAsync();
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(posted.Id, _sitter), default);
        return posted;
    }

    private async Task<RequestView?> CancelAsync(Guid requestId, Guid member)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CancelRequestHandler>()
            .HandleAsync(new CancelRequestCommand(requestId, member), default);
    }

    private async Task<RequestView?> WithdrawAsync(Guid requestId, Guid member)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WithdrawClaimHandler>()
            .HandleAsync(new WithdrawClaimCommand(requestId, member), default);
    }

    private async Task AssertRequesterBalanceAsync(decimal available, decimal escrowed)
    {
        await using var scope = _database.CreateScope();
        var account = await _database.NewContext(scope).CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == _requester);
        account.Available.Value.Should().Be(available);
        account.Escrowed.Value.Should().Be(escrowed);
    }

    private async Task<int> RefundCountAsync()
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).LedgerEntries.AsNoTracking().CountAsync(e => e.Kind == LedgerEntryKind.Refund);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_a_posted_or_claimed_request_refunds_it_in_full_with_one_ledger_entry(bool claimed)
    {
        var request = claimed ? await ClaimedAsync() : await PostAsync();

        var view = await CancelAsync(request.Id, _requester);

        view!.Status.Should().Be(RequestStatus.Cancelled);
        await AssertRequesterBalanceAsync(10m, 0m);
        (await RefundCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Cancelling_twice_refunds_only_once()
    {
        var request = await PostAsync();
        await CancelAsync(request.Id, _requester);

        var act = () => CancelAsync(request.Id, _requester);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("invalid_state");
        await AssertRequesterBalanceAsync(10m, 0m);
        (await RefundCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Cancelling_at_the_start_time_is_rejected_and_nothing_is_refunded()
    {
        var request = await ClaimedAsync();
        _database.Clock.Advance(TimeSpan.FromHours(10));

        var act = () => CancelAsync(request.Id, _requester);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("start_passed");
        await AssertRequesterBalanceAsync(8m, 2m);
        (await RefundCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancelling_after_payout_is_rejected()
    {
        var request = await ClaimedAsync();
        await using (var scope = _database.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRequestStore>();
            var stored = (await store.FindRequestAsync(request.Id, default))!;
            var requester = (await store.FindAccountAsync(_requester, default))!;
            var sitter = (await store.FindAccountAsync(_sitter, default))!;
            stored.Complete(requester, sitter, Now.AddHours(11));
            var record = await _database.NewContext(scope).Requests.SingleAsync();
            record.Status = stored.Status;
            await _database.NewContext(scope).SaveChangesAsync();
        }

        var act = () => CancelAsync(request.Id, _requester);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("invalid_state");
        (await RefundCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Only_the_requester_can_cancel_and_anyone_else_is_told_it_does_not_exist()
    {
        var request = await PostAsync();

        (await CancelAsync(request.Id, _sitter)).Should().BeNull();
        (await CancelAsync(Guid.NewGuid(), _requester)).Should().BeNull();

        await AssertRequesterBalanceAsync(8m, 2m);
    }

    [Fact]
    public async Task Withdrawing_before_the_claim_cutoff_returns_the_request_to_posted_and_hides_the_address()
    {
        var request = await ClaimedAsync();

        var view = await WithdrawAsync(request.Id, _sitter);

        view!.Status.Should().Be(RequestStatus.Posted);
        view.Address.Should().BeEmpty();
        await using var scope = _database.CreateScope();
        var stored = await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync();
        stored.Status.Should().Be(RequestStatus.Posted);
        stored.SitterId.Should().BeNull();
        await AssertRequesterBalanceAsync(8m, 2m);
        (await RefundCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_withdrawn_request_is_open_to_other_sitters_again()
    {
        var request = await ClaimedAsync();
        await WithdrawAsync(request.Id, _sitter);
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));

        await using var scope = _database.CreateScope();
        var open = await scope.ServiceProvider.GetRequiredService<IRequestReader>()
            .ListOpenAsync(other, _database.Clock.GetUtcNow().UtcDateTime, PageRequest.From(null, null), default);

        open.Items.Select(r => r.Id).Should().Equal(request.Id);
    }

    [Fact]
    public async Task Withdrawing_after_the_claim_cutoff_expires_the_request_and_refunds_the_requester()
    {
        var request = await ClaimedAsync();
        _database.Clock.SetUtcNow(new DateTimeOffset(request.ClaimCutoffAt, TimeSpan.Zero));

        var view = await WithdrawAsync(request.Id, _sitter);

        view!.Status.Should().Be(RequestStatus.Expired);
        await AssertRequesterBalanceAsync(10m, 0m);
        (await RefundCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Withdrawing_at_the_start_time_is_rejected()
    {
        var request = await ClaimedAsync();
        _database.Clock.Advance(TimeSpan.FromHours(10));

        var act = () => WithdrawAsync(request.Id, _sitter);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("start_passed");
    }

    [Fact]
    public async Task Only_the_sitter_can_withdraw_and_anyone_else_is_told_it_does_not_exist()
    {
        var request = await ClaimedAsync();

        (await WithdrawAsync(request.Id, _requester)).Should().BeNull();
        (await WithdrawAsync(Guid.NewGuid(), _sitter)).Should().BeNull();
    }

    [Fact]
    public async Task A_cancel_racing_a_withdraw_cannot_both_win()
    {
        var request = await ClaimedAsync();
        await using var first = _database.CreateScope();
        await using var second = _database.CreateScope();
        var firstStore = first.ServiceProvider.GetRequiredService<IRequestStore>();
        var secondStore = second.ServiceProvider.GetRequiredService<IRequestStore>();
        var cancelled = (await firstStore.FindRequestAsync(request.Id, default))!;
        var withdrawn = (await secondStore.FindRequestAsync(request.Id, default))!;
        var firstAccount = (await firstStore.FindAccountAsync(_requester, default))!;
        var secondAccount = (await secondStore.FindAccountAsync(_requester, default))!;
        cancelled.Cancel(_requester, firstAccount, Now);
        withdrawn.Withdraw(_sitter, secondAccount, Now);
        await firstStore.SaveReleasedAsync(cancelled, firstAccount, Now, default);

        var act = () => secondStore.SaveReleasedAsync(withdrawn, secondAccount, Now, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        (await RefundCountAsync()).Should().Be(1);
    }
}
