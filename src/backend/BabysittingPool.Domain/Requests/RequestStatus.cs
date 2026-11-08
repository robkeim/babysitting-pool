namespace BabysittingPool.Domain.Requests;

/// <summary>Where a Request is in its life (spec §4). Completed, Canceled and Expired are end states.</summary>
public enum RequestStatus
{
    Posted,
    Claimed,
    Completed,
    Canceled,
    Expired,
}
