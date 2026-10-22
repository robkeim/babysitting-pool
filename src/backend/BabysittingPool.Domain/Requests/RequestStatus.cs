namespace BabysittingPool.Domain.Requests;

/// <summary>Where a Request is in its lifecycle (credit-engine.md §4). Completed, Cancelled and Expired are final.</summary>
public enum RequestStatus
{
    Posted,
    Claimed,
    Completed,
    Cancelled,
    Expired,
}
