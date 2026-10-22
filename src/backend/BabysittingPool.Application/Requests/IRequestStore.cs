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
}

/// <summary>Read port: queries project straight from the database (ADR 0002).</summary>
public interface IRequestReader
{
    /// <summary>The Requester's Requests, newest posted first, starting after <paramref name="after"/>.</summary>
    Task<IReadOnlyList<RequestRead>> ListByRequesterAsync(
        Guid requesterId, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken);
}
