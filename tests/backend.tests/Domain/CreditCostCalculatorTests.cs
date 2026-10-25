using AwesomeAssertions;
using BabysittingPool.Domain.Credits;

namespace backend.tests.Domain;

public class CreditCostCalculatorTests
{
    private static decimal Cost(
        int minutes,
        int children = 1,
        BabysittingLocation location = BabysittingLocation.SittersHouse,
        TransportOption transport = TransportOption.None,
        MealOption meals = MealOption.None) =>
        CreditCostCalculator.Calculate(new CreditRequestDetails(minutes, children, location, transport, meals)).Total;

    [Theory]
    [InlineData(60, 1.0)]
    [InlineData(75, 1.25)]
    [InlineData(30, 0.5)]
    [InlineData(150, 2.5)]
    public void Base_cost_is_fractional_hours_at_one_credit_per_hour_for_first_child(int minutes, double expected)
    {
        Cost(minutes).Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.5)]
    [InlineData(3, 1.75)]
    [InlineData(4, 2.0)]
    [InlineData(5, 2.25)]
    public void Hourly_rate_scales_with_additional_children(int children, double expectedRate)
    {
        CreditCostCalculator.HourlyRate(children).Should().Be((decimal)expectedRate);
        Cost(60, children).Should().Be((decimal)expectedRate);
    }

    [Fact]
    public void Child_scaling_applies_per_hour()
    {
        // 2.5 hours * (1.0 + 0.5 + 0.25)
        Cost(150, children: 3).Should().Be(4.375m);
    }

    [Fact]
    public void Sitters_house_adds_no_premium()
    {
        Cost(60, location: BabysittingLocation.SittersHouse).Should().Be(1.0m);
    }

    [Fact]
    public void Requesting_familys_house_adds_flat_half_credit_regardless_of_duration()
    {
        Cost(60, location: BabysittingLocation.RequestingFamilysHouse).Should().Be(1.5m);
        Cost(240, location: BabysittingLocation.RequestingFamilysHouse).Should().Be(4.5m);
    }

    [Theory]
    [InlineData(TransportOption.None, 1.0)]
    [InlineData(TransportOption.OneWay, 1.5)]
    [InlineData(TransportOption.RoundTrip, 1.75)]
    public void Transport_adds_flat_fee(TransportOption transport, double expected)
    {
        Cost(60, transport: transport).Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(TransportOption.OneWay)]
    [InlineData(TransportOption.RoundTrip)]
    public void Transport_cannot_be_combined_with_requesting_familys_house(TransportOption transport)
    {
        var act = () => new CreditRequestDetails(60, 1, BabysittingLocation.RequestingFamilysHouse, transport);

        act.Should().Throw<DomainException>().WithMessage("*Transport*");
    }

    [Theory]
    [InlineData(MealOption.None, 1, 1.0)]
    [InlineData(MealOption.Lunch, 1, 1.5)]
    [InlineData(MealOption.Snack, 1, 1.25)]
    [InlineData(MealOption.Dinner, 1, 1.5)]
    [InlineData(MealOption.Lunch | MealOption.Dinner, 1, 2.0)]
    [InlineData(MealOption.Lunch | MealOption.Snack, 1, 1.75)]
    public void Meals_add_flat_fee_per_child(MealOption meals, int children, double expectedMealOnlyTotal)
    {
        Cost(60, children, meals: meals).Should().Be((decimal)expectedMealOnlyTotal);
    }

    [Fact]
    public void Meal_fee_is_multiplied_by_child_count_but_not_by_duration()
    {
        // 2h * (1.0 + 0.5) = 3.0, plus 2 children * 0.5 lunch = 1.0
        Cost(120, children: 2, meals: MealOption.Lunch).Should().Be(4.0m);
    }

    [Fact]
    public void All_components_combine()
    {
        // 90 min, 3 children: 1.5h * 1.75 = 2.625; family house +0.5; meals+snack 3 * 0.75 = 2.25
        var breakdown = CreditCostCalculator.Calculate(new CreditRequestDetails(
            90, 3, BabysittingLocation.RequestingFamilysHouse, TransportOption.None,
            MealOption.Lunch | MealOption.Snack));

        breakdown.Hours.Should().Be(1.5m);
        breakdown.HourlyRate.Should().Be(1.75m);
        breakdown.TimeCost.Should().Be(2.625m);
        breakdown.LocationPremium.Should().Be(0.5m);
        breakdown.TransportFee.Should().Be(0m);
        breakdown.MealFee.Should().Be(2.25m);
        breakdown.Total.Should().Be(5.375m);
    }

    [Fact]
    public void Calculation_is_deterministic()
    {
        var request = new CreditRequestDetails(90, 2, transport: TransportOption.RoundTrip, meals: MealOption.Snack);

        CreditCostCalculator.Calculate(request).Should().Be(CreditCostCalculator.Calculate(request));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-15, 1)]
    [InlineData(82, 1)]
    [InlineData(10, 1)]
    [InlineData(61, 1)]
    [InlineData(60, 0)]
    [InlineData(60, -1)]
    public void Invalid_requests_are_rejected(int minutes, int children)
    {
        var act = () => new CreditRequestDetails(minutes, children);

        act.Should().Throw<DomainException>();
    }
}
