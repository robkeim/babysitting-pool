namespace BabysittingPool.Domain.Requests;

/// <summary>Where a Request is in its life (spec §4). Completed, Cancelled and Expired are end states.</summary>
public enum RequestStatus
{
    Posted,
    Claimed,
    Completed,
    Cancelled,
    Expired,
}
