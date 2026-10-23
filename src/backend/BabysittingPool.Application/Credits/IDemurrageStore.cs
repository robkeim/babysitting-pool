using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Credits;

/// <summary>What Demurrage needs to know about one Member (spec §5).</summary>
/// <param name="Account">The Member's Credit Account; the store remembers the version it read.</param>
/// <param name="MemberSince">When the account was created, which starts a new Member's 21 days.</param>
/// <param name="LastCreditAt">When Available last grew (Grant, Refund or Payout), or null.</param>
/// <param name="ChargedBoundaries">The boundaries already charged to this Member.</param>
/// <param name="CompletedStartTimes">The start times of the Requests Completed as Requester or as Sitter.</param>
public sealed record DemurrageCandidate(
    CreditAccount Account,
    DateTime MemberSince,
    DateTime? LastCreditAt,
    IReadOnlyCollection<DateTime> ChargedBoundaries,
    IReadOnlyCollection<DateTime> CompletedStartTimes);

/// <summary>Write port for Demurrage; each Member is charged in one transaction (ADR 0004, 0007).</summary>
public interface IDemurrageStore
{
    /// <summary>The Member's Demurrage history, or null if they have no Credit Account.</summary>
    Task<DemurrageCandidate?> FindAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the Member's reduced account and one Demurrage Ledger entry per charge in one transaction. Throws
    /// <see cref="ConcurrencyConflictException"/> if the account changed since it was read.
    /// </summary>
    Task SaveChargedAsync(
        Guid memberId, CreditAccount account, IReadOnlyList<DemurrageCharge> charges, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Read port for Demurrage.</summary>
public interface IDemurrageReader
{
    /// <summary>The open (not closed) Members whose Available Balance is above zero, so there is something to charge.</summary>
    Task<IReadOnlyList<Guid>> ListChargeableAsync(CancellationToken cancellationToken);
}
