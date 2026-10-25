using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Requests;

/// <summary>
/// A Member's ask for childcare (spec §4): born Posted with its cost in Escrow, then Claimed, and ending
/// Completed, Cancelled or Expired. The Escrow belongs to the Request: every transition that releases it
/// leaves the Request in an end state, so it can be refunded or paid out at most once.
/// </summary>
public sealed class Request
{
    /// <summary>Start times fall on a boundary of this many minutes, with no seconds (spec §4.1).</summary>
    public const int StartIncrementMinutes = 5;

    public const int MaxAreaLength = 100;
    public const int MaxAddressLength = 200;

    private Request(
        Guid id, Guid requesterId, Guid? sitterId, CreditRequestDetails details, DateTime startsAt,
        string area, string address, Credits.Credits cost, DateTime claimCutoffAt, DateTime postedAt, RequestStatus status)
    {
        Id = id;
        RequesterId = requesterId;
        SitterId = sitterId;
        Details = details;
        StartsAt = startsAt;
        Area = area;
        Address = address;
        Cost = cost;
        ClaimCutoffAt = claimCutoffAt;
        PostedAt = postedAt;
        Status = status;
    }

    public Guid Id { get; }

    public Guid RequesterId { get; }

    public Guid? SitterId { get; private set; }

    public CreditRequestDetails Details { get; }

    public DateTime StartsAt { get; }

    /// <summary>The rough neighbourhood, which is all other Members see while the Request is Posted (spec §4.6).</summary>
    public string Area { get; }

    /// <summary>The exact address; only the Requester, and the Sitter once Claimed, may see it (spec §4.6).</summary>
    public string Address { get; }

    /// <summary>The cost taken into Escrow when the Request was posted.</summary>
    public Credits.Credits Cost { get; }

    /// <summary>Snapshotted when posted, so later changes to the setting never move a promised window (spec §6).</summary>
    public DateTime ClaimCutoffAt { get; }

    public DateTime PostedAt { get; }

    public RequestStatus Status { get; private set; }

    /// <summary>
    /// Posts a Request and escrows its cost from the Requester's Available Balance (spec §4.1).
    /// </summary>
    public static Request Post(
        Guid requesterId,
        CreditAccount requester,
        CreditRequestDetails details,
        DateTime startsAt,
        string area,
        string address,
        DateTime now,
        TimeSpan minimumLeadTime,
        TimeSpan claimCutoff)
    {
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(details);

        var cleanArea = RequireText(area, MaxAreaLength, "area");
        var cleanAddress = RequireText(address, MaxAddressLength, "address");
        if (startsAt.Ticks % TimeSpan.FromMinutes(StartIncrementMinutes).Ticks != 0)
        {
            throw new DomainException(
                $"A request must start on a {StartIncrementMinutes}-minute boundary.", "invalid_start_time");
        }

        if (startsAt - now < minimumLeadTime)
        {
            throw new DomainException(
                $"A request must start at least {minimumLeadTime} after it is posted.", "below_minimum_lead_time");
        }

        if (requester.Available <= 0m)
        {
            throw new DomainException(
                "A member with no available credits cannot post a request.", "no_available_credits");
        }

        var cost = Credits.Credits.FromDecimal(CreditCostCalculator.Calculate(details).Total);
        requester.Escrow(cost.Value);

        return new Request(
            Guid.NewGuid(), requesterId, null, details, startsAt, cleanArea, cleanAddress, cost,
            startsAt - claimCutoff, now, RequestStatus.Posted);
    }

    /// <summary>Rehydrates a Request from persisted state.</summary>
    public static Request Restore(
        Guid id, Guid requesterId, Guid? sitterId, CreditRequestDetails details, DateTime startsAt,
        string area, string address, Credits.Credits cost, DateTime claimCutoffAt, DateTime postedAt, RequestStatus status) =>
        new(id, requesterId, sitterId, details, startsAt, area, address, cost, claimCutoffAt, postedAt, status);

    /// <summary>
    /// The status a reader should show at <paramref name="now"/>: an overdue Posted Request already reads as
    /// Expired and an overdue Claimed one as Completed, before Settle has applied them (ADR 0007).
    /// </summary>
    public static RequestStatus StatusAt(RequestStatus stored, DateTime claimCutoffAt, DateTime startsAt, DateTime now) =>
        stored switch
        {
            RequestStatus.Posted when now >= claimCutoffAt => RequestStatus.Expired,
            RequestStatus.Claimed when now >= startsAt => RequestStatus.Completed,
            _ => stored,
        };

    public RequestStatus StatusNow(DateTime now) => StatusAt(Status, ClaimCutoffAt, StartsAt, now);

    /// <summary>The first Sitter to claim wins; there is no approval step (spec §4.4).</summary>
    public void Claim(Guid sitterId, DateTime now)
    {
        RequireStatus(RequestStatus.Posted, "claimed");
        if (sitterId == RequesterId)
        {
            throw new DomainException("A member cannot claim their own request.", "own_request");
        }

        if (now >= ClaimCutoffAt)
        {
            throw new DomainException("The Claim Cutoff has passed.", "claim_cutoff_passed");
        }

        SitterId = sitterId;
        Status = RequestStatus.Claimed;
    }

    /// <summary>
    /// The Sitter drops their claim before the start time (spec §4.5). The Request returns to Posted, or if the
    /// Claim Cutoff has passed it expires and the Requester is refunded.
    /// </summary>
    public void Withdraw(Guid sitterId, CreditAccount requester, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requester);
        RequireStatus(RequestStatus.Claimed, "withdrawn");
        if (SitterId != sitterId)
        {
            throw new DomainException("Only the Sitter who claimed the request can withdraw.", "not_the_sitter");
        }

        RequireBeforeStart(now);
        SitterId = null;
        if (now >= ClaimCutoffAt)
        {
            Refund(requester, RequestStatus.Expired);
            return;
        }

        Status = RequestStatus.Posted;
    }

    /// <summary>The Requester cancels a Posted or Claimed Request before its start time, with a full refund (spec §4.5).</summary>
    public void Cancel(Guid requesterId, CreditAccount requester, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requester);
        if (requesterId != RequesterId)
        {
            throw new DomainException("Only the Requester can cancel the request.", "not_the_requester");
        }

        if (Status is not (RequestStatus.Posted or RequestStatus.Claimed))
        {
            throw new DomainException($"A {Status} request cannot be cancelled.", "invalid_state");
        }

        RequireBeforeStart(now);
        Refund(requester, RequestStatus.Cancelled);
    }

    /// <summary>An unclaimed Request expires at its Claim Cutoff and the Requester is refunded (spec §4.2).</summary>
    public void Expire(CreditAccount requester, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requester);
        RequireStatus(RequestStatus.Posted, "expired");
        if (now < ClaimCutoffAt)
        {
            throw new DomainException("The Claim Cutoff has not passed yet.", "claim_cutoff_not_passed");
        }

        Refund(requester, RequestStatus.Expired);
    }

    /// <summary>When the start time arrives the Escrow is released to the Sitter (spec §4.3).</summary>
    public void Complete(CreditAccount requester, CreditAccount sitter, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(sitter);
        RequireStatus(RequestStatus.Claimed, "completed");
        if (now < StartsAt)
        {
            throw new DomainException("The start time has not arrived yet.", "start_not_reached");
        }

        requester.PayOut(Cost.Value, sitter);
        Status = RequestStatus.Completed;
    }

    private void Refund(CreditAccount requester, RequestStatus endState)
    {
        requester.Refund(Cost.Value);
        Status = endState;
    }

    private void RequireStatus(RequestStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"A {Status} request cannot be {action}.", "invalid_state");
        }
    }

    private void RequireBeforeStart(DateTime now)
    {
        if (now >= StartsAt)
        {
            throw new DomainException("The start time has passed.", "start_passed");
        }
    }

    private static string RequireText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} is required and at most {maxLength} characters.", $"invalid_{field}");
        }

        return trimmed;
    }
}
