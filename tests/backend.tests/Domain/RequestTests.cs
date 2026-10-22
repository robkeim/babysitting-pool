using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace backend.tests.Domain;

public class RequestTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddHours(10);
    private static readonly TimeSpan LeadTime = TimeSpan.FromHours(6);
    private static readonly TimeSpan CutoffBeforeStart = TimeSpan.FromHours(3);
    private static readonly DateTime Cutoff = Start - CutoffBeforeStart;

    // 2 hours, 1 child, at the sitter's house: 2.0 credits.
    private static readonly CreditRequestDetails TwoHours = new(durationMinutes: 120, childCount: 1);

    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _sitterId = Guid.NewGuid();
    private readonly CreditAccount _requester = CreditAccount.Open();
    private readonly CreditAccount _sitter = CreditAccount.Open();

    private Request Post(DateTime? startsAt = null) =>
        Request.Post(_requesterId, _requester, TwoHours, startsAt ?? Start, Now, LeadTime, CutoffBeforeStart);

    private Request Claimed()
    {
        var request = Post();
        request.Claim(_sitterId, Now.AddHours(1));
        return request;
    }

    [Fact]
    public void Posting_escrows_the_calculated_cost_and_snapshots_the_claim_cutoff()
    {
        var request = Post();

        request.Status.Should().Be(RequestStatus.Posted);
        request.Cost.Should().Be(2.0m);
        request.ClaimCutoff.Should().Be(Cutoff);
        _requester.Available.Should().Be(8.0m);
        _requester.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Posting_with_insufficient_balance_is_rejected_and_changes_nothing()
    {
        var poor = CreditAccount.Restore(1.75m, 0m);

        var act = () => Request.Post(_requesterId, poor, TwoHours, Start, Now, LeadTime, CutoffBeforeStart);

        act.Should().Throw<DomainException>();
        poor.Available.Should().Be(1.75m);
        poor.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Posting_with_zero_available_balance_is_rejected_with_a_clear_error()
    {
        var broke = CreditAccount.Restore(0m, 5m);

        var act = () => Request.Post(_requesterId, broke, TwoHours, Start, Now, LeadTime, CutoffBeforeStart);

        act.Should().Throw<DomainException>().WithMessage("*no available credits*");
    }

    [Fact]
    public void Posting_less_than_the_minimum_lead_time_ahead_is_rejected()
    {
        var act = () => Post(Now + LeadTime - TimeSpan.FromMinutes(1));

        act.Should().Throw<DomainException>();
        _requester.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Posting_exactly_the_minimum_lead_time_ahead_is_allowed()
    {
        var act = () => Post(Now + LeadTime);

        act.Should().NotThrow();
    }

    [Fact]
    public void Posting_with_a_claim_cutoff_not_after_the_posting_time_is_rejected()
    {
        var act = () => Request.Post(
            _requesterId, _requester, TwoHours, Start, Now, LeadTime, TimeSpan.FromHours(10));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Claiming_records_the_sitter()
    {
        var request = Post();

        request.Claim(_sitterId, Cutoff.AddMinutes(-1));

        request.Status.Should().Be(RequestStatus.Claimed);
        request.SitterId.Should().Be(_sitterId);
    }

    [Fact]
    public void Requester_cannot_claim_their_own_request()
    {
        var request = Post();

        var act = () => request.Claim(_requesterId, Now);

        act.Should().Throw<DomainException>();
        request.Status.Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public void Claiming_at_or_after_the_claim_cutoff_is_rejected()
    {
        var request = Post();

        var act = () => request.Claim(_sitterId, Cutoff);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_claimed_request_cannot_be_claimed_again()
    {
        var request = Claimed();

        var act = () => request.Claim(Guid.NewGuid(), Now.AddHours(2));

        act.Should().Throw<DomainException>();
        request.SitterId.Should().Be(_sitterId);
    }

    [Fact]
    public void Cancelling_a_posted_request_refunds_the_escrow()
    {
        var request = Post();

        request.Cancel(_requesterId, _requester, Now.AddHours(1));

        request.Status.Should().Be(RequestStatus.Cancelled);
        _requester.Available.Should().Be(10.0m);
        _requester.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Cancelling_a_claimed_request_refunds_the_escrow()
    {
        var request = Claimed();

        request.Cancel(_requesterId, _requester, Start.AddMinutes(-1));

        request.Status.Should().Be(RequestStatus.Cancelled);
        _requester.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Only_the_requester_can_cancel()
    {
        var request = Post();

        var act = () => request.Cancel(_sitterId, _requester, Now.AddHours(1));

        act.Should().Throw<DomainException>();
        request.Status.Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public void Cancelling_at_or_after_the_start_time_is_rejected()
    {
        var request = Claimed();

        var act = () => request.Cancel(_requesterId, _requester, Start);

        act.Should().Throw<DomainException>();
        _requester.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Cancelling_twice_refunds_only_once()
    {
        var request = Post();
        request.Cancel(_requesterId, _requester, Now.AddHours(1));

        var act = () => request.Cancel(_requesterId, _requester, Now.AddHours(2));

        act.Should().Throw<DomainException>();
        _requester.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Expiring_an_unclaimed_request_at_the_cutoff_refunds_the_escrow()
    {
        var request = Post();

        request.Expire(_requester, Cutoff);

        request.Status.Should().Be(RequestStatus.Expired);
        _requester.Available.Should().Be(10.0m);
        _requester.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Expiring_before_the_cutoff_is_rejected()
    {
        var request = Post();

        var act = () => request.Expire(_requester, Cutoff.AddMinutes(-1));

        act.Should().Throw<DomainException>();
        request.Status.Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public void A_claimed_request_does_not_expire()
    {
        var request = Claimed();

        var act = () => request.Expire(_requester, Cutoff);

        act.Should().Throw<DomainException>();
        _requester.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Refund_after_expiry_is_rejected()
    {
        var request = Post();
        request.Expire(_requester, Cutoff);

        var cancel = () => request.Cancel(_requesterId, _requester, Cutoff.AddMinutes(1));
        var expire = () => request.Expire(_requester, Cutoff.AddMinutes(2));

        cancel.Should().Throw<DomainException>();
        expire.Should().Throw<DomainException>();
        _requester.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Withdrawing_before_the_cutoff_returns_the_request_to_posted()
    {
        var request = Claimed();

        request.Withdraw(_sitterId, _requester, Cutoff.AddMinutes(-1));

        request.Status.Should().Be(RequestStatus.Posted);
        request.SitterId.Should().BeNull();
        _requester.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Withdrawing_after_the_cutoff_expires_the_request_and_refunds_the_escrow()
    {
        var request = Claimed();

        request.Withdraw(_sitterId, _requester, Cutoff);

        request.Status.Should().Be(RequestStatus.Expired);
        request.SitterId.Should().BeNull();
        _requester.Available.Should().Be(10.0m);
        _requester.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Withdrawing_at_or_after_the_start_time_is_rejected()
    {
        var request = Claimed();

        var act = () => request.Withdraw(_sitterId, _requester, Start);

        act.Should().Throw<DomainException>();
        request.Status.Should().Be(RequestStatus.Claimed);
    }

    [Fact]
    public void Only_the_claiming_sitter_can_withdraw()
    {
        var request = Claimed();

        var act = () => request.Withdraw(Guid.NewGuid(), _requester, Now.AddHours(2));

        act.Should().Throw<DomainException>();
        request.SitterId.Should().Be(_sitterId);
    }

    [Fact]
    public void A_withdrawn_request_can_be_claimed_by_another_sitter()
    {
        var request = Claimed();
        request.Withdraw(_sitterId, _requester, Now.AddHours(2));
        var other = Guid.NewGuid();

        request.Claim(other, Now.AddHours(3));

        request.SitterId.Should().Be(other);
    }

    [Fact]
    public void Completing_at_the_start_time_pays_the_escrow_to_the_sitter()
    {
        var request = Claimed();

        request.Complete(_requester, _sitter, Start);

        request.Status.Should().Be(RequestStatus.Completed);
        _requester.Available.Should().Be(8.0m);
        _requester.Escrowed.Should().Be(0m);
        _sitter.Available.Should().Be(12.0m);
    }

    [Fact]
    public void Completing_before_the_start_time_is_rejected()
    {
        var request = Claimed();

        var act = () => request.Complete(_requester, _sitter, Start.AddMinutes(-1));

        act.Should().Throw<DomainException>();
        _sitter.Available.Should().Be(10.0m);
    }

    [Fact]
    public void An_unclaimed_request_cannot_be_completed()
    {
        var request = Post();

        var act = () => request.Complete(_requester, _sitter, Start);

        act.Should().Throw<DomainException>();
        _sitter.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Paying_out_twice_pays_only_once()
    {
        var request = Claimed();
        request.Complete(_requester, _sitter, Start);

        var act = () => request.Complete(_requester, _sitter, Start.AddMinutes(1));

        act.Should().Throw<DomainException>();
        _sitter.Available.Should().Be(12.0m);
    }

    [Fact]
    public void Cancel_and_withdraw_after_completion_are_rejected_and_do_not_refund()
    {
        var request = Claimed();
        request.Complete(_requester, _sitter, Start);

        var cancel = () => request.Cancel(_requesterId, _requester, Start.AddMinutes(1));
        var withdraw = () => request.Withdraw(_sitterId, _requester, Start.AddMinutes(1));

        cancel.Should().Throw<DomainException>();
        withdraw.Should().Throw<DomainException>();
        _requester.Available.Should().Be(8.0m);
    }

    [Fact]
    public void Completing_a_cancelled_request_is_rejected()
    {
        var request = Claimed();
        request.Cancel(_requesterId, _requester, Now.AddHours(2));

        var act = () => request.Complete(_requester, _sitter, Start);

        act.Should().Throw<DomainException>();
        _sitter.Available.Should().Be(10.0m);
        _requester.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Completing_an_expired_request_is_rejected()
    {
        var request = Post();
        request.Expire(_requester, Cutoff);

        var act = () => request.Complete(_requester, _sitter, Start);

        act.Should().Throw<DomainException>();
        _sitter.Available.Should().Be(10.0m);
    }
}
