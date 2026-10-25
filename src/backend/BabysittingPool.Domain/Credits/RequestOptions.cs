namespace BabysittingPool.Domain.Credits;

public enum BabysittingLocation
{
    SittersHouse,
    RequestingFamilysHouse,
}

public enum TransportOption
{
    None,
    OneWay,
    RoundTrip,
}

[Flags]
public enum MealOption
{
    None = 0,
    Lunch = 1,
    Snack = 2,
    Dinner = 4,
}
