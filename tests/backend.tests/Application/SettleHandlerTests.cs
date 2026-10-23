using AwesomeAssertions;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class SettleHandlerTests : IAsyncLifetime
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

    private async Task<RequestView> PostAsync(Guid requester, int hoursAhead = 10)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            requester, Now.AddHours(hoursAhead), new CreditRequestDetails(120, 1), "Riverside", "12 River Rd");
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task ClaimAsync(Guid requestId, Guid sitter)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(requestId, sitter), default);
    }

    private async Task<SettleResult> SettleAsync()
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
    }

    private async Task<CreditAccountRecord> AccountAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId);
    }

    private async Task<RequestStatus> StatusAsync(Guid requestId)
    {
        await using var scope = _database.CreateScope();
        return (await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync(r => r.Id == requestId)).Status;
    }

    private async Task<List<LedgerEntryRecord>> LedgerAsync(Guid requestId, LedgerEntryKind kind)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).LedgerEntries.AsNoTracking()
            .Where(e => e.RequestId == requestId && e.Kind == kind).ToListAsync();
    }

    [Fact]
    public async Task An_unclaimed_request_past_its_claim_cutoff_expires_and_the_escrow_is_refunded()
    {
        var posted = await PostAsync(_requester);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt, TimeSpan.Zero));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(1, 0, 0, 0));
        (await StatusAsync(posted.Id)).Should().Be(RequestStatus.Expired);
        var account = await AccountAsync(_requester);
        account.Available.Value.Should().Be(10m);
        account.Escrowed.Value.Should().Be(0m);
        var refund = (await LedgerAsync(posted.Id, LedgerEntryKind.Refund)).Single();
        refund.MemberId.Should().Be(_requester);
        refund.Amount.Value.Should().Be(2m);
        refund.OccurredAt.Should().Be(posted.ClaimCutoffAt);
    }

    [Fact]
    public async Task A_posted_request_before_its_claim_cutoff_is_left_alone()
    {
        var posted = await PostAsync(_requester);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt.AddTicks(-1), TimeSpan.Zero));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 0, 0, 0));
        (await StatusAsync(posted.Id)).Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public async Task A_claimed_request_past_its_start_is_completed_and_the_escrow_goes_to_the_sitter()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.StartsAt, TimeSpan.Zero));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 1, 0, 0));
        (await StatusAsync(posted.Id)).Should().Be(RequestStatus.Completed);
        var requester = await AccountAsync(_requester);
        requester.Available.Value.Should().Be(8m);
        requester.Escrowed.Value.Should().Be(0m);
        (await AccountAsync(_sitter)).Available.Value.Should().Be(12m);
        var payout = (await LedgerAsync(posted.Id, LedgerEntryKind.Payout)).Single();
        payout.MemberId.Should().Be(_sitter);
        payout.Amount.Value.Should().Be(2m);
    }

    [Fact]
    public async Task A_claimed_request_past_its_claim_cutoff_but_before_its_start_is_left_alone()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.StartsAt.AddTicks(-1), TimeSpan.Zero));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(0, 0, 0, 0));
        (await StatusAsync(posted.Id)).Should().Be(RequestStatus.Claimed);
        (await AccountAsync(_requester)).Escrowed.Value.Should().Be(2m);
    }

    [Fact]
    public async Task A_repeat_run_does_nothing()
    {
        var expiring = await PostAsync(_requester);
        var paying = await PostAsync(_requester, hoursAhead: 12);
        await ClaimAsync(paying.Id, _sitter);
        _database.Clock.Advance(TimeSpan.FromHours(13));
        await SettleAsync();
        var requester = await AccountAsync(_requester);
        var sitter = await AccountAsync(_sitter);

        var again = await SettleAsync();

        again.Should().Be(new SettleResult(0, 0, 0, 0));
        (await AccountAsync(_requester)).Should().BeEquivalentTo(requester);
        (await AccountAsync(_sitter)).Should().BeEquivalentTo(sitter);
        (await LedgerAsync(expiring.Id, LedgerEntryKind.Refund)).Should().HaveCount(1);
        (await LedgerAsync(paying.Id, LedgerEntryKind.Payout)).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_request_that_cannot_be_settled_is_counted_and_does_not_block_the_others()
    {
        var healthy = await PostAsync(_requester);
        var withoutAccount = await AccountSeeder.SeedMemberAsync(_database.Services, _database.Clock);
        await using (var scope = _database.CreateScope())
        {
            // A Request whose Requester has no Credit Account cannot be refunded; it must not stop the rest.
            var db = _database.NewContext(scope);
            db.Requests.Add(new RequestRecord
            {
                Id = Guid.NewGuid(),
                RequesterId = withoutAccount,
                StartsAt = Now.AddHours(10),
                DurationMinutes = 120,
                ChildCount = 1,
                Area = "Hilltop",
                Address = "1 Hill St",
                Cost = Credits.FromDecimal(2m),
                ClaimCutoffAt = Now.AddHours(7),
                PostedAt = Now,
                Status = RequestStatus.Posted,
            });
            await db.SaveChangesAsync();
        }

        _database.Clock.Advance(TimeSpan.FromHours(8));

        var result = await SettleAsync();

        result.Should().Be(new SettleResult(1, 0, 0, 1));
        (await StatusAsync(healthy.Id)).Should().Be(RequestStatus.Expired);
    }
}
