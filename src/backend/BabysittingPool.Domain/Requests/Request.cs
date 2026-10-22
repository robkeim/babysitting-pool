using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Requests;

/// <summary>
/// A Member's posted ask for childcare (credit-engine.md §4). Its Escrow is tied to the Request: the state machine
/// guarantees the Escrow is refunded or paid out exactly once. Clock-driven transitions take <c>now</c> as a parameter.
/// </summary>
public sealed class Request
{
    private Request(
        Guid id,
        Guid requesterId,
        Guid? sitterId,
        CreditRequestDetails details,
        decimal cost,
        DateTime postedAt,
        DateTime startsAt,
        DateTime claimCutoff,
        RequestStatus status)
    {
        Id = id;
        RequesterId = requesterId;
        SitterId = sitterId;
        Details = details;
        Cost = cost;
        PostedAt = postedAt;
        StartsAt = startsAt;
        ClaimCutoff = claimCutoff;
        Status = status;
    }

    public Guid Id { get; }
    public Guid RequesterId { get; }
    public Guid? SitterId { get; private set; }
    public CreditRequestDetails Details { get; }

    /// <summary>The Credits escrowed when the Request was posted.</summary>
    public decimal Cost { get; }

    public DateTime PostedAt { get; }
    public DateTime StartsAt { get; }

    /// <summary>Snapshot taken at posting, so later setting changes never move a promised window (spec §6).</summary>
    public DateTime ClaimCutoff { get; }

    public RequestStatus Status { get; private set; }

    /// <summary>
    /// Posts a Request and escrows its cost from the Requester. A Request is born Posted; there is no draft state.
    /// </summary>
    public static Request Post(
        Guid requesterId,
        CreditAccount requesterAccount,
        CreditRequestDetails details,
        DateTime startsAt,
        DateTime now,
        TimeSpan minimumLeadTime,
        TimeSpan claimCutoffBeforeStart)
    {
        ArgumentNullException.ThrowIfNull(requesterAccount);
        ArgumentNullException.ThrowIfNull(details);

        if (startsAt < now + minimumLeadTime)
        {
            throw new DomainException(
                $"A request must start at least {minimumLeadTime.TotalHours:0.##} hours after it is posted.");
        }

        var claimCutoff = startsAt - claimCutoffBeforeStart;
        if (claimCutoff <= now)
        {
            throw new DomainException("The claim cutoff must fall after the time of posting.");
        }

        if (requesterAccount.Available <= 0m)
        {
            throw new DomainException("Members with no available credits cannot post requests.");
        }

        var cost = CreditCostCalculator.Calculate(details).Total;
        requesterAccount.Escrow(cost);

        return new Request(
            Guid.NewGuid(), requesterId, null, details, cost, now, startsAt, claimCutoff, RequestStatus.Posted);
    }

    public static Request Restore(
        Guid id,
        Guid requesterId,
        Guid? sitterId,
        CreditRequestDetails details,
        decimal cost,
        DateTime postedAt,
        DateTime startsAt,
        DateTime claimCutoff,
        RequestStatus status) =>
        new(id, requesterId, sitterId, details, cost, postedAt, startsAt, claimCutoff, status);

    /// <summary>The first Sitter to claim wins; no approval step (spec §4.4).</summary>
    public void Claim(Guid sitterId, DateTime now)
    {
        RequireStatus(RequestStatus.Posted, "claimed");
        if (sitterId == RequesterId)
        {
            throw new DomainException("A member cannot claim their own request.");
        }

        if (now >= ClaimCutoff)
        {
            throw new DomainException("The claim cutoff has passed.");
        }

        SitterId = sitterId;
        Status = RequestStatus.Claimed;
    }

    /// <summary>
    /// The Sitter drops their claim before the start time. The Request returns to Posted if the Claim Cutoff has
    /// not passed; otherwise it expires and the Escrow is refunded (spec §4.5).
    /// </summary>
    public void Withdraw(Guid sitterId, CreditAccount requesterAccount, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requesterAccount);
        RequireStatus(RequestStatus.Claimed, "withdrawn from");
        if (sitterId != SitterId)
        {
            throw new DomainException("Only the sitter who claimed the request can withdraw.");
        }

        RequireBeforeStart(now);

        SitterId = null;
        if (now < ClaimCutoff)
        {
            Status = RequestStatus.Posted;
            return;
        }

        requesterAccount.Refund(Cost);
        Status = RequestStatus.Expired;
    }

    /// <summary>The Requester cancels a Posted or Claimed Request before the start time; Escrow is refunded in full.</summary>
    public void Cancel(Guid requesterId, CreditAccount requesterAccount, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requesterAccount);
        if (Status is not (RequestStatus.Posted or RequestStatus.Claimed))
        {
            throw new DomainException($"A {Status} request cannot be cancelled.");
        }

        if (requesterId != RequesterId)
        {
            throw new DomainException("Only the requester can cancel the request.");
        }

        RequireBeforeStart(now);

        requesterAccount.Refund(Cost);
        Status = RequestStatus.Cancelled;
    }

    /// <summary>An unclaimed Request expires at its Claim Cutoff and its Escrow is refunded (applied by Settle).</summary>
    public void Expire(CreditAccount requesterAccount, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requesterAccount);
        RequireStatus(RequestStatus.Posted, "expired");
        if (now < ClaimCutoff)
        {
            throw new DomainException("The claim cutoff has not passed yet.");
        }

        requesterAccount.Refund(Cost);
        Status = RequestStatus.Expired;
    }

    /// <summary>When the start time arrives the Escrow is released to the Sitter (applied by Settle).</summary>
    public void Complete(CreditAccount requesterAccount, CreditAccount sitterAccount, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(requesterAccount);
        ArgumentNullException.ThrowIfNull(sitterAccount);
        RequireStatus(RequestStatus.Claimed, "completed");
        if (now < StartsAt)
        {
            throw new DomainException("The start time has not arrived yet.");
        }

        requesterAccount.PayOut(Cost, sitterAccount);
        Status = RequestStatus.Completed;
    }

    private void RequireStatus(RequestStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"A {Status} request cannot be {action}.");
        }
    }

    private void RequireBeforeStart(DateTime now)
    {
        if (now >= StartsAt)
        {
            throw new DomainException("The start time has already arrived.");
        }
    }
}
