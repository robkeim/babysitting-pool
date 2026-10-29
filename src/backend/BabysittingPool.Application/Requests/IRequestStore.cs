using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Application.Requests;

/// <summary>A concurrent change got there first (ADR 0004); the caller's work was not saved.</summary>
public sealed class ConcurrencyConflictException(Exception inner) : Exception("Concurrent update.", inner);

/// <summary>Write port for Requests and the Credit Account movements they cause.</summary>
public interface IRequestStore
{
    /// <summary>The Member's Credit Account, or null. The store remembers the version it read.</summary>
    Task<CreditAccount?> FindAccountAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>The Member's area and address from their profile, or null if they have not set them.</summary>
    Task<HomeLocation?> FindHomeLocationAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a newly posted Request, the Requester's updated account, the Escrow Ledger entry and a Posted
    /// Notification for every other open Member in one transaction, and returns those Notifications for the caller
    /// to announce once it has committed. Throws <see cref="ConcurrencyConflictException"/> if the account changed since it was read.
    /// </summary>
    Task<IReadOnlyList<NotificationDelivery>> AddPostedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken);

    /// <summary>The Request, or null. The store remembers the version it read (ADR 0004).</summary>
    Task<Request?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the database write lock for the claim that follows, so two claims by the same Sitter run one after the
    /// other and the overlap check sees the first (spec §4.7). Released by <see cref="SaveClaimedAsync"/>, or
    /// rolled back when the unit of work ends without saving.
    /// </summary>
    Task BeginClaimAsync(CancellationToken cancellationToken);

    /// <summary>The Requests the Sitter currently holds as Claimed, for the overlap rule (spec §4.7).</summary>
    Task<IReadOnlyList<Request>> FindClaimedBySitterAsync(Guid sitterId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Request's Claim and the Claimed Notification for its Requester in one transaction, and returns it for the caller to announce. Throws <see cref="ConcurrencyConflictException"/> if the Request changed since it
    /// was read, which is how the loser of a claim race finds out (ADR 0004).
    /// </summary>
    Task<IReadOnlyList<NotificationDelivery>> SaveClaimedAsync(Request request, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Request a Requester cancelled or a Sitter withdrew from. When the Request ended Cancelled or Expired
    /// the Requester's refunded account and the Refund Ledger entry go in the same transaction. A Cancelled Request
    /// that had a Sitter also stores a Cancelled Notification for them, returned for the caller to announce. Throws
    /// <see cref="ConcurrencyConflictException"/> if the Request or account changed since it was read.
    /// </summary>
    Task<IReadOnlyList<NotificationDelivery>> SaveReleasedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken);

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
    /// <summary>
    /// The Requester's Requests with each one's state at <paramref name="now"/>, newest posted first, one page as
    /// <paramref name="page"/> asks (spec §4).
    /// </summary>
    Task<Page<RequestView>> ListByRequesterAsync(
        Guid requesterId, DateTime now, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// "Open Requests" (spec §4.4, §6): Posted Requests still before their Claim Cutoff at <paramref name="now"/>,
    /// other than the Member's own, earliest start first, rough area only.
    /// </summary>
    Task<Page<OpenRequestView>> ListOpenAsync(
        Guid excludeRequesterId, DateTime now, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// The Requests the Member claimed as Sitter, latest start first, exact address included: the Sitter holds the
    /// claim (spec §4.6). A Request the Sitter withdrew from has no Sitter any more, so it drops off the list.
    /// </summary>
    Task<Page<RequestView>> ListClaimedByAsync(
        Guid sitterId, DateTime now, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The Posted Requests whose Claim Cutoff has been reached at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<Guid>> ListDueForExpiryAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>The Claimed Requests whose start time has been reached at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<Guid>> ListDueForPayoutAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>The Posted or Claimed Requests the Member posted or claimed, for Offboarding.</summary>
    Task<IReadOnlyList<Guid>> ListOpenInvolvingAsync(Guid memberId, CancellationToken cancellationToken);
}
