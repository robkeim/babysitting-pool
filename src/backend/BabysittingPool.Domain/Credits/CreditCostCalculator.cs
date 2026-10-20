namespace BabysittingPool.Domain.Credits;

/// <summary>Deterministic cost calculation per docs/specs/credit-engine.md §3.</summary>
public static class CreditCostCalculator
{
    public const decimal FirstChildHourlyRate = 1.0m;
    public const decimal SecondChildHourlyRate = 0.50m;
    public const decimal AdditionalChildHourlyRate = 0.25m;

    public const decimal RequestingFamilysHousePremium = 0.50m;

    public const decimal OneWayTransportFee = 0.50m;
    public const decimal RoundTripTransportFee = 0.75m;

    public const decimal LunchOrDinnerFeePerChild = 0.50m;
    public const decimal SnackFeePerChild = 0.25m;

    public static CreditCostBreakdown Calculate(CreditRequestDetails request)
    {
        var hours = request.DurationMinutes / 60m;
        var hourlyRate = HourlyRate(request.ChildCount);

        var locationPremium = request.Location == BabysittingLocation.RequestingFamilysHouse
            ? RequestingFamilysHousePremium
            : 0m;

        var transportFee = request.Transport switch
        {
            TransportOption.None => 0m,
            TransportOption.OneWay => OneWayTransportFee,
            TransportOption.RoundTrip => RoundTripTransportFee,
            _ => throw new DomainException($"Unknown transport option '{request.Transport}'."),
        };

        var mealFeePerChild =
            (request.Meals.HasFlag(MealOption.LunchOrDinner) ? LunchOrDinnerFeePerChild : 0m) +
            (request.Meals.HasFlag(MealOption.Snack) ? SnackFeePerChild : 0m);

        return new CreditCostBreakdown(
            Hours: hours,
            HourlyRate: hourlyRate,
            TimeCost: hours * hourlyRate,
            LocationPremium: locationPremium,
            TransportFee: transportFee,
            MealFee: mealFeePerChild * request.ChildCount);
    }

    /// <summary>Combined per-hour rate: 1st child 1.0, 2nd +0.50, each additional +0.25.</summary>
    public static decimal HourlyRate(int childCount) => childCount switch
    {
        < 1 => throw new DomainException("A request must include at least one child."),
        1 => FirstChildHourlyRate,
        _ => FirstChildHourlyRate + SecondChildHourlyRate + (childCount - 2) * AdditionalChildHourlyRate,
    };
}
