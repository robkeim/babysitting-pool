using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record CancelRequestCommand(Guid RequestId, Guid RequesterId);

/// <summary>The Requester cancels a Posted or Claimed Request before its start time and is refunded in full (spec §4.5).</summary>
public sealed class CancelRequestHandler(IRequestStore store, TimeProvider clock, NotificationAnnouncer announcer)
{
    /// <summary>
    /// The cancelled Request, or null when it does not exist or is not the caller's (ADR 0014). Rule violations
    /// throw <see cref="DomainException"/>; a concurrent change throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<RequestView?> HandleAsync(CancelRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await store.FindRequestAsync(command.RequestId, cancellationToken) is not { } request
            || request.RequesterId != command.RequesterId
            || await store.FindAccountAsync(request.RequesterId, cancellationToken) is not { } requester)
        {
            return null;
        }

        request.Cancel(command.RequesterId, requester, now);
        announcer.Created(await store.SaveReleasedAsync(request, requester, now, cancellationToken));

        return RequestRead.From(request).ToView(now);
    }
}
