using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Application.Requests;

/// <summary>
/// A Request as its Requester sees it, exact address and instructions included when it has them (spec §4.6). <see cref="SitterDisplayName"/>
/// names the Sitter of a Claimed Request and is set only when the Requester lists their Requests; it is how the
/// Claimed Notification shows who claimed (ADR 0019).
/// </summary>
public sealed record RequestView(
    Guid Id,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    bool Lunch,
    bool Snack,
    bool Dinner,
    string? Area,
    string? Address,
    string? Instructions,
    long CostQuarterCredits,
    DateTime ClaimCutoffAt,
    DateTime PostedAt,
    RequestStatus Status,
    string? SitterDisplayName = null);

/// <summary>
/// A Posted Request as other Members browse it: the rough area only (none at the Sitter's house), never the exact
/// address, its instructions or the Requester (spec §4.6).
/// </summary>
public sealed record OpenRequestView(
    Guid Id,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    bool Lunch,
    bool Snack,
    bool Dinner,
    string? Area,
    long CostQuarterCredits,
    DateTime ClaimCutoffAt);

/// <summary>A stored Request row as the read port returns it, before the status is projected for the clock.</summary>
public sealed record RequestRead(
    Guid Id,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    MealOption Meals,
    string? Area,
    string? Address,
    string? Instructions,
    long CostQuarterCredits,
    DateTime ClaimCutoffAt,
    DateTime PostedAt,
    RequestStatus Status)
{
    public static RequestRead From(Request request) => new(
        request.Id, request.StartsAt, request.Details.DurationMinutes, request.Details.ChildCount,
        request.Details.Location, request.Details.Transport, request.Details.Meals, request.Area, request.Address,
        request.Instructions,
        request.Cost.QuarterCredits, request.ClaimCutoffAt, request.PostedAt, request.Status);

    public RequestView ToView(DateTime now, string? sitterDisplayName = null) => new(
        Id, StartsAt, DurationMinutes, ChildCount, Location, Transport,
        Meals.HasFlag(MealOption.Lunch), Meals.HasFlag(MealOption.Snack), Meals.HasFlag(MealOption.Dinner),
        Area, Address, Instructions, CostQuarterCredits, ClaimCutoffAt, PostedAt,
        Request.StatusAt(Status, ClaimCutoffAt, StartsAt, now), sitterDisplayName);

    public OpenRequestView ToOpenView() => new(
        Id, StartsAt, DurationMinutes, ChildCount, Location, Transport,
        Meals.HasFlag(MealOption.Lunch), Meals.HasFlag(MealOption.Snack), Meals.HasFlag(MealOption.Dinner),
        Area, CostQuarterCredits, ClaimCutoffAt);
}

/// <summary>What a Request would cost, from the same calculator posting uses (spec §3).</summary>
public sealed record CostQuote(long CostQuarterCredits, CostLines Lines, CostOptionPrices Options);

/// <summary>The receipt: what each cost driver adds to this quote, in quarter-credits. The lines sum to the total.</summary>
public sealed record CostLines(
    long HourlyRateQuarterCredits,
    long TimeQuarterCredits,
    long LocationQuarterCredits,
    long TransportQuarterCredits,
    long LunchQuarterCredits,
    long SnackQuarterCredits,
    long DinnerQuarterCredits);

/// <summary>What each option would add at this child count, whether or not it is selected, so the form can price its own labels.</summary>
public sealed record CostOptionPrices(
    long FamilyHouseQuarterCredits,
    long OneWayTransportQuarterCredits,
    long RoundTripTransportQuarterCredits,
    long LunchQuarterCredits,
    long SnackQuarterCredits,
    long DinnerQuarterCredits,
    long LunchPerChildQuarterCredits,
    long SnackPerChildQuarterCredits,
    long DinnerPerChildQuarterCredits,
    long SecondChildHourlyQuarterCredits,
    long AdditionalChildHourlyQuarterCredits);
