using BabysittingPool.Domain.Credits;
using FluentAssertions;

namespace backend.tests.Domain;

public class CreditAccountTests
{
    [Fact]
    public void New_account_starts_with_ten_credits()
    {
        var account = CreditAccount.Open();

        account.Available.Should().Be(10.0m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Escrow_moves_credits_from_available()
    {
        var account = CreditAccount.Open();

        account.Escrow(3.5m);

        account.Available.Should().Be(6.5m);
        account.Escrowed.Should().Be(3.5m);
    }

    [Fact]
    public void Escrow_is_blocked_when_balance_is_insufficient()
    {
        var account = CreditAccount.Restore(2.0m, 0m);

        var act = () => account.Escrow(2.25m);

        act.Should().Throw<DomainException>();
        account.Available.Should().Be(2.0m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Escrow_of_entire_balance_is_allowed_and_reaches_the_zero_floor()
    {
        var account = CreditAccount.Restore(2.0m, 0m);

        account.Escrow(2.0m);

        account.Available.Should().Be(0m);
        account.CanSubmitRequests.Should().BeFalse();
    }

    [Fact]
    public void Refund_returns_escrow_to_available()
    {
        var account = CreditAccount.Open();
        account.Escrow(4m);

        account.Refund(4m);

        account.Available.Should().Be(10m);
        account.Escrowed.Should().Be(0m);
    }

    [Fact]
    public void Payout_transfers_escrow_to_sitter()
    {
        var requester = CreditAccount.Open();
        var sitter = CreditAccount.Open();
        requester.Escrow(4m);

        requester.PayOut(4m, sitter);

        requester.Available.Should().Be(6m);
        requester.Escrowed.Should().Be(0m);
        sitter.Available.Should().Be(14m);
    }

    [Fact]
    public void Cannot_pay_out_to_the_same_account()
    {
        var account = CreditAccount.Open();
        account.Escrow(4m);

        var act = () => account.PayOut(4m, account);

        act.Should().Throw<DomainException>();
        account.Escrowed.Should().Be(4m);
    }

    [Fact]
    public void Cannot_release_more_than_is_escrowed()
    {
        var requester = CreditAccount.Open();
        requester.Escrow(1m);

        var refund = () => requester.Refund(2m);
        var payout = () => requester.PayOut(2m, CreditAccount.Open());

        refund.Should().Throw<DomainException>();
        payout.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amounts_must_be_positive(double amount)
    {
        var account = CreditAccount.Open();

        var act = () => account.Escrow((decimal)amount);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Restore_rejects_negative_balances()
    {
        var act = () => CreditAccount.Restore(-0.25m, 0m);

        act.Should().Throw<DomainException>();
    }
}
