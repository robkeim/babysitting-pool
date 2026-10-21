using BabysittingPool.Domain.Credits;
using FluentAssertions;

namespace backend.tests.Domain;

public class CreditsTests
{
    [Fact]
    public void Credits_round_trip_through_quarter_credits()
    {
        var credits = Credits.FromQuarterCredits(41);

        credits.QuarterCredits.Should().Be(41);
        credits.Value.Should().Be(10.25m);
        Credits.FromDecimal(10.25m).Should().Be(credits);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(10.3)]
    [InlineData(-0.25)]
    public void Credits_must_be_non_negative_multiples_of_a_quarter(double amount)
    {
        var act = () => Credits.FromDecimal((decimal)amount);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Negative_quarter_credits_are_rejected()
    {
        var act = () => Credits.FromQuarterCredits(-1);

        act.Should().Throw<DomainException>();
    }
}
