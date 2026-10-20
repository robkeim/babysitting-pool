namespace BabysittingPool.Domain.Credits;

public sealed record CreditCostBreakdown(
    decimal Hours,
    decimal HourlyRate,
    decimal TimeCost,
    decimal LocationPremium,
    decimal TransportFee,
    decimal MealFee)
{
    public decimal Total => TimeCost + LocationPremium + TransportFee + MealFee;
}
