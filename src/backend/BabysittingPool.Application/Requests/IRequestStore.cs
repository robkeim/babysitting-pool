using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Application.Requests;

/// <summary>A concurrent change got there first (ADR 0004); the caller's work was not saved.</summary>
public sealed class ConcurrencyConflictException(Exception inner) : Exception("Concurrent update.", inner);

/// <summary>Write port for Requests and the Credit Account movements they cause.</summary>
public interface IRequestStore
{
    /// <summary>The Member's Credit Account, or null. The store remembers the version it read.</summary>
    Task<CreditAccount?> FindAccountAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a newly posted Request, the Requester's updated account and the Escrow Ledger entry in one
    /// transaction. Throws <see cref="ConcurrencyConflictException"/> if the account changed since it was read.
    /// </summary>
    Task AddPostedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken);

    /// <summary>The Request, or null. The store remembers the version it read (ADR 0004).</summary>
    Task<Request?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Request's Claim. Throws <see cref="ConcurrencyConflictException"/> if the Request changed since it
    /// was read, which is how the loser of a claim race finds out (ADR 0004).
    /// </summary>
    Task SaveClaimedAsync(Request request, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Request a Requester cancelled or a Sitter withdrew from. When the Request ended Cancelled or Expired
    /// the Requester's refunded account and the Refund Ledger entry go in the same transaction. Throws
    /// <see cref="ConcurrencyConflictException"/> if the Request or account changed since it was read.
    /// </summary>
    Task SaveReleasedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Saves an Expired Request, the Requester's refunded account and the Refund Ledger entry in one transaction.
    /// Throws <see cref="ConcurrencyConflictException"/> if the Request or account changed since it was read.
    /// </summary>
    Task SaveExpiredAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the account emptied by Offboarding and the Burn Ledger entry for <paramref name="burned"/> in one
    /// transaction. Throws <see cref="ConcurrencyConflictException"/> if the account changed since it was read.
    /// </summary>
    Task SaveBurnedAsync(Guid memberId, CreditAccount account, decimal burned, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Completed Request, both accounts and the Payout Ledger entry in one transaction. Throws
    /// <see cref="ConcurrencyConflictException"/> if the Request or an account changed since it was read.
    /// </summary>
    Task SaveCompletedAsync(
        Request request, CreditAccount requester, CreditAccount sitter, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Read port: queries project straight from the database (ADR 0002).</summary>
public interface IRequestReader
{
    /// <summary>The Requester's Requests, newest posted first, starting after <paramref name="after"/>.</summary>
    Task<IReadOnlyList<RequestRead>> ListByRequesterAsync(
        Guid requesterId, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Posted Requests still before their Claim Cutoff at <paramref name="now"/>, other than the Member's own,
    /// earliest start first, starting after <paramref name="after"/>.
    /// </summary>
    Task<IReadOnlyList<RequestRead>> ListOpenAsync(
        Guid excludeRequesterId, DateTime now, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken);

    /// <summary>The Requests the Member claimed as Sitter, latest start first, starting after <paramref name="after"/>.</summary>
    Task<IReadOnlyList<RequestRead>> ListClaimedByAsync(
        Guid sitterId, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken);

    /// <summary>The Posted Requests whose Claim Cutoff has been reached at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<Guid>> ListDueForExpiryAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>The Claimed Requests whose start time has been reached at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<Guid>> ListDueForPayoutAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>The Posted or Claimed Requests the Member posted or claimed, for Offboarding.</summary>
    Task<IReadOnlyList<Guid>> ListOpenInvolvingAsync(Guid memberId, CancellationToken cancellationToken);
}
