using AwesomeAssertions;
using BabysittingPool.Application;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Offboarding;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Application;

public class OffboardMemberHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _leaver;
    private Guid _other;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _leaver = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));
        _other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<RequestView> PostAsync(Guid requester, int hoursAhead = 10)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            requester, Now.AddHours(hoursAhead), new CreditRequestDetails(120, 1));
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task ClaimAsync(Guid requestId, Guid sitter)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(requestId, sitter), default);
    }

    private async Task<OffboardMemberResult?> OffboardAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OffboardMemberHandler>()
            .HandleAsync(new OffboardMemberCommand(memberId), default);
    }

    private async Task<CreditAccountRecord> AccountAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId);
    }

    private async Task<RequestRecord> RequestAsync(Guid requestId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync(r => r.Id == requestId);
    }

    private async Task<MemberRecord> MemberAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).Members.AsNoTracking().SingleAsync(m => m.Id == memberId);
    }

    private async Task<List<LedgerEntryRecord>> LedgerAsync(Guid memberId, LedgerEntryKind kind)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).LedgerEntries.AsNoTracking()
            .Where(e => e.MemberId == memberId && e.Kind == kind).ToListAsync();
    }

    [Fact]
    public async Task Offboarding_closes_the_account_burns_the_balance_and_keeps_the_ledger()
    {
        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 0, 0, 40));
        (await MemberAsync(_leaver)).ClosedAt.Should().Be(Now);
        (await AccountAsync(_leaver)).Available.Value.Should().Be(0m);
        var burn = (await LedgerAsync(_leaver, LedgerEntryKind.Burn)).Single();
        burn.Amount.Value.Should().Be(10m);
        burn.OccurredAt.Should().Be(Now);
        burn.RequestId.Should().BeNull();
        (await LedgerAsync(_leaver, LedgerEntryKind.Grant)).Should().ContainSingle();
    }

    [Fact]
    public async Task Offboarding_ends_the_members_sessions()
    {
        var secret = Secrets.Generate();
        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdentityStore>()
                .AddSessionAsync(Session.Start(_leaver, Secrets.Hash(secret), Now), default);
        }

        await OffboardAsync(_leaver);

        await using var after = _database.CreateScope();
        (await after.ServiceProvider.GetRequiredService<ResolveSessionHandler>().HandleAsync(secret, default)).Should().BeNull();
        (await _database.NewContext(after).Sessions.CountAsync(s => s.MemberId == _leaver)).Should().Be(0);
    }

    [Fact]
    public async Task An_account_with_nothing_to_burn_gets_no_burn_entry()
    {
        var broke = await _database.SeedAccountAsync(Credits.FromQuarterCredits(0), Credits.FromQuarterCredits(0));

        var result = await OffboardAsync(broke);

        result.Should().Be(new OffboardMemberResult(0, 0, 0, 0));
        (await LedgerAsync(broke, LedgerEntryKind.Burn)).Should().BeEmpty();
        (await MemberAsync(broke)).IsAdmin.Should().BeFalse();
        (await MemberAsync(broke)).ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_posted_request_is_canceled_and_its_escrow_refunded_before_the_burn()
    {
        var posted = await PostAsync(_leaver);

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(1, 0, 0, 40));
        (await RequestAsync(posted.Id)).Status.Should().Be(RequestStatus.Canceled);
        var account = await AccountAsync(_leaver);
        account.Available.Value.Should().Be(0m);
        account.Escrowed.Value.Should().Be(0m);
        (await LedgerAsync(_leaver, LedgerEntryKind.Refund)).Should().ContainSingle().Which.Amount.Value.Should().Be(2m);
        (await LedgerAsync(_leaver, LedgerEntryKind.Burn)).Single().Amount.Value.Should().Be(10m);
    }

    [Fact]
    public async Task A_posted_request_past_its_claim_cutoff_is_expired_instead()
    {
        var posted = await PostAsync(_leaver);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt, TimeSpan.Zero));

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 0, 1, 40));
        (await RequestAsync(posted.Id)).Status.Should().Be(RequestStatus.Expired);
    }

    [Fact]
    public async Task A_request_the_member_posted_that_is_claimed_is_canceled_and_the_sitter_is_free()
    {
        var posted = await PostAsync(_leaver);
        await ClaimAsync(posted.Id, _other);

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(1, 0, 0, 40));
        var request = await RequestAsync(posted.Id);
        request.Status.Should().Be(RequestStatus.Canceled);
        (await AccountAsync(_other)).Available.Value.Should().Be(10m);
    }

    [Fact]
    public async Task A_claim_the_member_holds_is_withdrawn_and_the_request_returns_to_posted()
    {
        var posted = await PostAsync(_other);
        await ClaimAsync(posted.Id, _leaver);

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 1, 0, 40));
        var request = await RequestAsync(posted.Id);
        request.Status.Should().Be(RequestStatus.Posted);
        request.SitterId.Should().BeNull();
        (await AccountAsync(_other)).Escrowed.Value.Should().Be(2m);
    }

    [Fact]
    public async Task A_claim_withdrawn_past_the_claim_cutoff_expires_the_request_and_refunds_the_requester()
    {
        var posted = await PostAsync(_other);
        await ClaimAsync(posted.Id, _leaver);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt, TimeSpan.Zero));

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 1, 0, 40));
        (await RequestAsync(posted.Id)).Status.Should().Be(RequestStatus.Expired);
        var requester = await AccountAsync(_other);
        requester.Available.Value.Should().Be(10m);
        requester.Escrowed.Value.Should().Be(0m);
    }

    [Fact]
    public async Task A_claimed_request_that_has_already_started_is_paid_out_not_canceled()
    {
        var posted = await PostAsync(_other);
        await ClaimAsync(posted.Id, _leaver);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.StartsAt, TimeSpan.Zero));

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 0, 1, 48));
        (await RequestAsync(posted.Id)).Status.Should().Be(RequestStatus.Completed);
        (await AccountAsync(_other)).Escrowed.Value.Should().Be(0m);
        (await LedgerAsync(_leaver, LedgerEntryKind.Payout)).Should().ContainSingle();
        (await AccountAsync(_leaver)).Available.Value.Should().Be(0m);
    }

    [Fact]
    public async Task Requests_that_are_already_over_and_other_peoples_requests_are_left_alone()
    {
        var done = await PostAsync(_leaver, hoursAhead: 7);
        await ClaimAsync(done.Id, _other);
        _database.Clock.Advance(TimeSpan.FromHours(8));
        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
        }

        var theirs = await PostAsync(_other);

        var result = await OffboardAsync(_leaver);

        result.Should().Be(new OffboardMemberResult(0, 0, 0, 32));
        (await RequestAsync(done.Id)).Status.Should().Be(RequestStatus.Completed);
        (await RequestAsync(theirs.Id)).Status.Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public async Task A_request_that_completes_or_is_canceled_after_the_list_was_read_is_skipped()
    {
        var completing = await PostAsync(_leaver, hoursAhead: 7);
        await ClaimAsync(completing.Id, _other);
        var canceling = await PostAsync(_leaver);
        var beforeBurn = await AccountAsync(_leaver);

        // The Requests finish between the list and the release: the Requester cancels one, Settle pays the other.
        var reader = new RacingReader(_database.Services.GetRequiredService<IRequestReader>(), async () =>
        {
            await using var scope = _database.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CancelRequestHandler>()
                .HandleAsync(new CancelRequestCommand(canceling.Id, _leaver), default);
            _database.Clock.Advance(TimeSpan.FromHours(8));
            await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
        });
        var handler = new OffboardMemberHandler(
            _database.Services.GetRequiredService<IIdentityStore>(), reader,
            _database.Services.GetRequiredService<IServiceScopeFactory>(), _database.Clock);

        var result = await handler.HandleAsync(new OffboardMemberCommand(_leaver), default);

        reader.Listed.Should().BeEquivalentTo([completing.Id, canceling.Id]);
        result.Should().NotBeNull();
        result!.Canceled.Should().Be(0);
        result.Withdrawn.Should().Be(0);
        result.Settled.Should().Be(0);
        (await RequestAsync(completing.Id)).Status.Should().Be(RequestStatus.Completed);
        (await RequestAsync(canceling.Id)).Status.Should().Be(RequestStatus.Canceled);
        (await LedgerAsync(_leaver, LedgerEntryKind.Burn)).Should().ContainSingle();
        (await AccountAsync(_leaver)).Available.Value.Should().Be(0m);
        beforeBurn.Available.Value.Should().BeGreaterThan(0m);
    }

    private sealed class RacingReader(IRequestReader inner, Func<Task> meanwhile) : IRequestReader
    {
        public IReadOnlyList<Guid> Listed { get; private set; } = [];

        public async Task<IReadOnlyList<Guid>> ListOpenInvolvingAsync(Guid memberId, CancellationToken cancellationToken)
        {
            Listed = await inner.ListOpenInvolvingAsync(memberId, cancellationToken);
            await meanwhile();
            return Listed;
        }

        public Task<Page<RequestView>> ListByRequesterAsync(Guid requesterId, DateTime now, PageRequest page, CancellationToken cancellationToken) =>
            inner.ListByRequesterAsync(requesterId, now, page, cancellationToken);

        public Task<Page<OpenRequestView>> ListOpenAsync(Guid excludeRequesterId, DateTime now, PageRequest page, CancellationToken cancellationToken) =>
            inner.ListOpenAsync(excludeRequesterId, now, page, cancellationToken);

        public Task<Page<RequestView>> ListClaimedByAsync(Guid sitterId, DateTime now, PageRequest page, CancellationToken cancellationToken) =>
            inner.ListClaimedByAsync(sitterId, now, page, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListDueForExpiryAsync(DateTime now, CancellationToken cancellationToken) =>
            inner.ListDueForExpiryAsync(now, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListDueForPayoutAsync(DateTime now, CancellationToken cancellationToken) =>
            inner.ListDueForPayoutAsync(now, cancellationToken);
    }

    [Fact]
    public async Task Offboarding_twice_does_nothing_the_second_time()
    {
        await PostAsync(_leaver);
        await OffboardAsync(_leaver);
        var closedAt = (await MemberAsync(_leaver)).ClosedAt;
        _database.Clock.Advance(TimeSpan.FromHours(1));

        var again = await OffboardAsync(_leaver);

        again.Should().Be(new OffboardMemberResult(0, 0, 0, 0));
        (await MemberAsync(_leaver)).ClosedAt.Should().Be(closedAt);
        (await LedgerAsync(_leaver, LedgerEntryKind.Burn)).Should().ContainSingle();
        (await LedgerAsync(_leaver, LedgerEntryKind.Refund)).Should().ContainSingle();
    }

    [Fact]
    public async Task The_admin_cannot_be_offboarded()
    {
        await using (var scope = _database.CreateScope())
        {
            var db = _database.NewContext(scope);
            (await db.Members.SingleAsync(m => m.Id == _leaver)).IsAdmin = true;
            await db.SaveChangesAsync();
        }

        var act = () => OffboardAsync(_leaver);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("cannot_offboard_admin");
        (await MemberAsync(_leaver)).ClosedAt.Should().BeNull();
        (await AccountAsync(_leaver)).Available.Value.Should().Be(10m);
    }

    [Fact]
    public async Task An_unknown_member_is_not_found()
    {
        (await OffboardAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task A_returning_person_gets_a_brand_new_member_with_a_fresh_starting_balance()
    {
        var email = (await MemberAsync(_leaver)).Email;
        await OffboardAsync(_leaver);
        EmailAddress.TryParse(email, out var address).Should().BeTrue();
        DisplayName.TryCreate("Ada Again", out var name).Should().BeTrue();

        await using (var scope = _database.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<InvitationIssuer>()
                .HandleAsync(new IssueInvitationCommand(address, false), default)).Should().Be(IssueInvitationResult.Issued);
        }

        var token = System.Web.HttpUtility.ParseQueryString(_database.Outbox.Messages.Last(m => m.To == email).Link.Query)["token"]!;
        await using (var scope = _database.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<AcceptInvitationHandler>()
                .HandleAsync(new AcceptInvitationCommand(token, name), default)).Should().NotBeNull();
        }

        await using var check = _database.CreateScope();
        var db = _database.NewContext(check);
        var returning = await db.Members.AsNoTracking().SingleAsync(m => m.Email == email && m.ClosedAt == null);
        returning.Id.Should().NotBe(_leaver);
        (await AccountAsync(returning.Id)).Available.Value.Should().Be(10m);
        (await AccountAsync(_leaver)).Available.Value.Should().Be(0m);
        (await LedgerAsync(_leaver, LedgerEntryKind.Burn)).Should().ContainSingle();
    }
}
