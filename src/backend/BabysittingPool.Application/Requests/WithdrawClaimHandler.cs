using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record WithdrawClaimCommand(Guid RequestId, Guid SitterId);

/// <summary>
/// The Sitter drops their claim before the start time (spec §4.5). The Request returns to Posted, or if the Claim
/// Cutoff has passed it expires and the Requester is refunded.
/// </summary>
public sealed class WithdrawClaimHandler(IRequestStore store, TimeProvider clock, NotificationAnnouncer announcer)
{
    /// <summary>
    /// The Request as it now stands, or null when it does not exist or the caller is not its Sitter
    /// (ADR 0014). Rule violations throw <see cref="DomainException"/>; a concurrent change throws
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<RequestView?> HandleAsync(WithdrawClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await store.FindRequestAsync(command.RequestId, cancellationToken) is not { } request
            || request.SitterId != command.SitterId
            || await store.FindAccountAsync(request.RequesterId, cancellationToken) is not { } requester)
        {
            return null;
        }

        request.Withdraw(command.SitterId, requester, now);
        announcer.Created(await store.SaveReleasedAsync(request, requester, now, cancellationToken));

        // The exact address, its instructions and the notes are the Sitter's only while the claim stands (spec §4.6): answer with the area alone.
        return RequestRead.From(request).ToView(now) with { Address = null, Instructions = null, Notes = null };
    }
}
