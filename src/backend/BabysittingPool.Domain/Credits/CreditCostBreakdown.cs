namespace BabysittingPool.Domain.Credits;

public sealed record CreditCostBreakdown(
    decimal Hours,
    decimal HourlyRate,
    decimal TimeCost,
    decimal LocationPremium,
    decimal TransportFee,
    decimal MealFee,
    decimal LunchFee,
    decimal SnackFee,
    decimal DinnerFee)
{
    public decimal Total => TimeCost + LocationPremium + TransportFee + MealFee;
}

/// <summary>Per-option prices at a given child count, selected or not.</summary>
public sealed record CreditOptionPrices(
    decimal FamilyHouse,
    decimal OneWayTransport,
    decimal RoundTripTransport,
    decimal LunchPerChild,
    decimal SnackPerChild,
    decimal DinnerPerChild,
    decimal Lunch,
    decimal Snack,
    decimal Dinner,
    decimal SecondChildHourly,
    decimal AdditionalChildHourly);
