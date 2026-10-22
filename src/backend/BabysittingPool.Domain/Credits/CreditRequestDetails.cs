namespace BabysittingPool.Domain.Credits;

/// <summary>The inputs that determine the cost of a babysitting request (spec §3).</summary>
public sealed record CreditRequestDetails
{
    /// <summary>Durations are booked in whole increments of this many minutes (spec §3A).</summary>
    public const int DurationIncrementMinutes = 15;

    public CreditRequestDetails(
        int durationMinutes,
        int childCount,
        BabysittingLocation location = BabysittingLocation.SittersHouse,
        TransportOption transport = TransportOption.None,
        MealOption meals = MealOption.None)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainException("Duration must be greater than zero minutes.", "invalid_duration");
        }

        if (durationMinutes % DurationIncrementMinutes != 0)
        {
            throw new DomainException(
                $"Duration must be a multiple of {DurationIncrementMinutes} minutes.",
                "invalid_duration");
        }

        if (childCount < 1)
        {
            throw new DomainException("A request must include at least one child.", "invalid_child_count");
        }

        if (location == BabysittingLocation.RequestingFamilysHouse && transport != TransportOption.None)
        {
            throw new DomainException(
                "Transport fees cannot be combined with the requesting family's house location premium.",
                "transport_with_family_house");
        }

        DurationMinutes = durationMinutes;
        ChildCount = childCount;
        Location = location;
        Transport = transport;
        Meals = meals;
    }

    public int DurationMinutes { get; }
    public int ChildCount { get; }
    public BabysittingLocation Location { get; }
    public TransportOption Transport { get; }
    public MealOption Meals { get; }
}
