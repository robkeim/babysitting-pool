using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace backend.tests.Domain;

public class RequestTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan LeadTime = TimeSpan.FromHours(6);
    private static readonly TimeSpan Cutoff = TimeSpan.FromHours(3);
    private static readonly DateTime Start = Now.AddHours(10);

    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _sitterId = Guid.NewGuid();

    // Two hours for one child at the sitter's house costs 2.0 credits.
    private static CreditRequestDetails TwoHours => new(durationMinutes: 120, childCount: 1);

    private Request Post(CreditAccount account, DateTime? startsAt = null) =>
        Request.Post(
            _requesterId, account, TwoHours, startsAt ?? Start, " Riverside ", "12 River Rd", Now, LeadTime, Cutoff);

    private Request Claimed(CreditAccount account)
    {
        var request = Post(account);
        request.Claim(_sitterId, Now, []);
        return request;
    }

    [Fact]
    public void Posting_escrows_the_calculated_cost_and_snapshots_the_claim_cutoff()
    {
        var account = CreditAccount.Open();

        var request = Post(account);

        request.Status.Should().Be(RequestStatus.Posted);
        request.Cost.Value.Should().Be(2.0m);
        request.ClaimCutoffAt.Should().Be(Start.AddHours(-3));
        request.PostedAt.Should().Be(Now);
        request.Area.Should().Be("Riverside");
        account.Available.Should().Be(8.0m);
        account.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Posting_at_exactly_the_minimum_lead_time_is_allowed()
    {
        var act = () => Post(CreditAccount.Open(), Now + LeadTime);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(0, 0, 30)]
    [InlineData(0, 4, 59)]
    public void Posting_off_the_five_minute_boundary_is_blocked_and_escrows_nothing(int minutes, int seconds, int millis)
    {
        var account = CreditAccount.Open();

        var act = () => Post(account, Start + new TimeSpan(0, 0, minutes, seconds, millis));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_start_time");
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Posting_on_a_five_minute_boundary_is_allowed()
    {
        var act = () => Post(CreditAccount.Open(), Start.AddMinutes(35));

        act.Should().NotThrow();
    }

    [Fact]
    public void Posting_inside_the_minimum_lead_time_is_blocked_and_escrows_nothing()
    {
        var account = CreditAccount.Open();

        var act = () => Post(account, Now + LeadTime - TimeSpan.FromMinutes(15));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("below_minimum_lead_time");
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Posting_with_zero_available_credits_is_blocked_with_a_clear_code()
    {
        var account = CreditAccount.Restore(0m, 5m);

        var act = () => Post(account);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("no_available_credits");
    }

    [Fact]
    public void Posting_with_less_than_the_cost_available_is_blocked()
    {
        var account = CreditAccount.Restore(1.75m, 0m);

        var act = () => Post(account);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("insufficient_credits");
        account.Available.Should().Be(1.75m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Posting_with_exactly_the_cost_available_is_allowed()
    {
        var account = CreditAccount.Restore(2.0m, 0m);

        Post(account);

        account.Available.Should().Be(0m);
    }

    [Theory]
    [InlineData("", "12 River Rd", "invalid_area")]
    [InlineData("Riverside", "  ", "invalid_address")]
    public void Posting_requires_an_area_and_an_address(string area, string address, string code)
    {
        var account = CreditAccount.Open();

        var act = () => Request.Post(_requesterId, account, TwoHours, Start, area, address, Now, LeadTime, Cutoff);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void The_first_claim_makes_the_request_claimed()
    {
        var request = Post(CreditAccount.Open());

        request.Claim(_sitterId, Now, []);

        request.Status.Should().Be(RequestStatus.Claimed);
        request.SitterId.Should().Be(_sitterId);
    }

    [Fact]
    public void A_request_cannot_be_claimed_twice()
    {
        var request = Claimed(CreditAccount.Open());

        var act = () => request.Claim(Guid.NewGuid(), Now, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
    }

    [Fact]
    public void Requester_and_sitter_must_be_different_members()
    {
        var request = Post(CreditAccount.Open());

        var act = () => request.Claim(_requesterId, Now, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("own_request");
        request.Status.Should().Be(RequestStatus.Posted);
    }

    [Fact]
    public void A_request_cannot_be_claimed_at_or_after_the_claim_cutoff()
    {
        var request = Post(CreditAccount.Open());

        var act = () => request.Claim(_sitterId, request.ClaimCutoffAt, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("claim_cutoff_passed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelling_a_posted_or_claimed_request_refunds_the_escrow(bool claimed)
    {
        var account = CreditAccount.Open();
        var request = claimed ? Claimed(account) : Post(account);

        request.Cancel(_requesterId, account, Now.AddHours(1));

        request.Status.Should().Be(RequestStatus.Cancelled);
        account.Available.Should().Be(10.0m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Only_the_requester_can_cancel()
    {
        var account = CreditAccount.Open();
        var request = Post(account);

        var act = () => request.Cancel(_sitterId, account, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("not_the_requester");
        account.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void A_request_cannot_be_cancelled_once_the_start_time_has_arrived()
    {
        var account = CreditAccount.Open();
        var request = Claimed(account);

        var act = () => request.Cancel(_requesterId, account, Start);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("start_passed");
    }

    [Fact]
    public void Cancelling_twice_does_not_refund_twice()
    {
        var account = CreditAccount.Open();
        var request = Post(account);
        request.Cancel(_requesterId, account, Now);

        var act = () => request.Cancel(_requesterId, account, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
        account.Available.Should().Be(10.0m);
    }

    [Fact]
    public void An_unclaimed_request_expires_at_the_claim_cutoff_and_is_refunded()
    {
        var account = CreditAccount.Open();
        var request = Post(account);

        request.Expire(account, request.ClaimCutoffAt);

        request.Status.Should().Be(RequestStatus.Expired);
        account.Available.Should().Be(10.0m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void A_request_cannot_expire_before_its_claim_cutoff()
    {
        var account = CreditAccount.Open();
        var request = Post(account);

        var act = () => request.Expire(account, request.ClaimCutoffAt.AddTicks(-1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("claim_cutoff_not_passed");
    }

    [Fact]
    public void A_claimed_request_cannot_expire()
    {
        var account = CreditAccount.Open();
        var request = Claimed(account);

        var act = () => request.Expire(account, request.ClaimCutoffAt);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
        account.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void A_cancelled_request_cannot_expire_so_it_is_not_refunded_twice()
    {
        var account = CreditAccount.Open();
        var request = Post(account);
        request.Cancel(_requesterId, account, Now);

        var act = () => request.Expire(account, request.ClaimCutoffAt);

        act.Should().Throw<DomainException>();
        account.Available.Should().Be(10.0m);
    }

    [Fact]
    public void Withdrawing_before_the_cutoff_returns_the_request_to_posted_and_keeps_the_escrow()
    {
        var account = CreditAccount.Open();
        var request = Claimed(account);

        request.Withdraw(_sitterId, account, Now.AddHours(1));

        request.Status.Should().Be(RequestStatus.Posted);
        request.SitterId.Should().BeNull();
        account.Escrowed.Should().Be(2.0m);
    }

    [Fact]
    public void Withdrawing_after_the_cutoff_expires_the_request_and_refunds_it()
    {
        var account = CreditAccount.Open();
        var request = Claimed(account);

        request.Withdraw(_sitterId, account, request.ClaimCutoffAt);

        request.Status.Should().Be(RequestStatus.Expired);
        account.Available.Should().Be(10.0m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Only_the_claiming_sitter_can_withdraw()
    {
        var account = CreditAccount.Open();
        var request = Claimed(account);

        var act = () => request.Withdraw(Guid.NewGuid(), account, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("not_the_sitter");
    }

    [Fact]
    public void Completing_at_the_start_time_pays_the_escrow_to_the_sitter()
    {
        var requester = CreditAccount.Open();
        var sitter = CreditAccount.Restore(1.0m, 0m);
        var request = Claimed(requester);

        request.Complete(requester, sitter, Start);

        request.Status.Should().Be(RequestStatus.Completed);
        requester.Escrowed.Should().Be(0m);
        requester.Available.Should().Be(8.0m);
        sitter.Available.Should().Be(3.0m);
    }

    [Fact]
    public void A_request_cannot_be_completed_before_its_start_time()
    {
        var requester = CreditAccount.Open();
        var request = Claimed(requester);

        var act = () => request.Complete(requester, CreditAccount.Open(), Start.AddTicks(-1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("start_not_reached");
    }

    [Fact]
    public void A_posted_request_cannot_be_completed()
    {
        var requester = CreditAccount.Open();
        var request = Post(requester);

        var act = () => request.Complete(requester, CreditAccount.Open(), Start);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
    }

    [Fact]
    public void Completing_twice_does_not_pay_twice()
    {
        var requester = CreditAccount.Open();
        var sitter = CreditAccount.Open();
        var request = Claimed(requester);
        request.Complete(requester, sitter, Start);

        var act = () => request.Complete(requester, sitter, Start);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
        sitter.Available.Should().Be(12.0m);
    }

    [Fact]
    public void A_completed_request_cannot_be_cancelled_or_refunded()
    {
        var requester = CreditAccount.Open();
        var request = Claimed(requester);
        request.Complete(requester, CreditAccount.Open(), Start);

        var cancel = () => request.Cancel(_requesterId, requester, Start);
        var expire = () => request.Expire(requester, Start);

        cancel.Should().Throw<DomainException>();
        expire.Should().Throw<DomainException>();
        requester.Available.Should().Be(8.0m);
    }

    [Fact]
    public void A_cancelled_request_cannot_be_completed_or_claimed()
    {
        var requester = CreditAccount.Open();
        var request = Claimed(requester);
        request.Cancel(_requesterId, requester, Now);

        var complete = () => request.Complete(requester, CreditAccount.Open(), Start);
        var claim = () => request.Claim(Guid.NewGuid(), Now, []);

        complete.Should().Throw<DomainException>();
        claim.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(RequestStatus.Posted, 0, RequestStatus.Posted)]
    [InlineData(RequestStatus.Posted, 7, RequestStatus.Expired)]
    [InlineData(RequestStatus.Claimed, 7, RequestStatus.Claimed)]
    [InlineData(RequestStatus.Claimed, 10, RequestStatus.Completed)]
    [InlineData(RequestStatus.Cancelled, 10, RequestStatus.Cancelled)]
    public void Readers_project_overdue_requests_before_settle_runs(RequestStatus stored, int hoursAfterPosting, RequestStatus shown)
    {
        var projected = Request.StatusAt(stored, Start.AddHours(-3), Start, Now.AddHours(hoursAfterPosting));

        projected.Should().Be(shown);
    }
}
