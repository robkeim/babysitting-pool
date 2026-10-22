using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Application.Requests;

/// <summary>A Request as its Requester sees it, exact address included (spec §4.6).</summary>
public sealed record RequestView(
    Guid Id,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    bool LunchOrDinner,
    bool Snack,
    string Area,
    string Address,
    long CostQuarterCredits,
    DateTime ClaimCutoffAt,
    DateTime PostedAt,
    RequestStatus Status);

/// <summary>
/// A Posted Request as other Members browse it: the rough area only, never the exact address or the Requester
/// (spec §4.6).
/// </summary>
public sealed record OpenRequestView(
    Guid Id,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    bool LunchOrDinner,
    bool Snack,
    string Area,
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
    string Area,
    string Address,
    long CostQuarterCredits,
    DateTime ClaimCutoffAt,
    DateTime PostedAt,
    RequestStatus Status)
{
    public static RequestRead From(Request request) => new(
        request.Id, request.StartsAt, request.Details.DurationMinutes, request.Details.ChildCount,
        request.Details.Location, request.Details.Transport, request.Details.Meals, request.Area, request.Address,
        request.Cost.QuarterCredits, request.ClaimCutoffAt, request.PostedAt, request.Status);

    public RequestView ToView(DateTime now) => new(
        Id, StartsAt, DurationMinutes, ChildCount, Location, Transport,
        Meals.HasFlag(MealOption.LunchOrDinner), Meals.HasFlag(MealOption.Snack),
        Area, Address, CostQuarterCredits, ClaimCutoffAt, PostedAt,
        Request.StatusAt(Status, ClaimCutoffAt, StartsAt, now));

    public OpenRequestView ToOpenView() => new(
        Id, StartsAt, DurationMinutes, ChildCount, Location, Transport,
        Meals.HasFlag(MealOption.LunchOrDinner), Meals.HasFlag(MealOption.Snack),
        Area, CostQuarterCredits, ClaimCutoffAt);
}

/// <summary>What a Request would cost, from the same calculator posting uses (spec §3).</summary>
public sealed record CostQuote(long CostQuarterCredits);
