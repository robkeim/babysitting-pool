using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Application.Requests;

public sealed record ClaimRequestCommand(Guid RequestId, Guid SitterId);

/// <summary>The first Sitter to claim a Posted Request wins; there is no approval step (spec §4.4).</summary>
public sealed partial class ClaimRequestHandler(
    IRequestStore store, TimeProvider clock, NotificationAnnouncer announcer, ILogger<ClaimRequestHandler> logger)
{
    /// <summary>
    /// The claimed Request as its Sitter sees it, or null when it does not exist. Rule violations throw
    /// <see cref="DomainException"/>; losing the race throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<RequestView?> HandleAsync(ClaimRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        await store.BeginClaimAsync(cancellationToken);
        if (await store.FindRequestAsync(command.RequestId, cancellationToken) is not { } request)
        {
            return null;
        }

        var sitterClaims = await store.FindClaimedBySitterAsync(command.SitterId, cancellationToken);
        request.Claim(command.SitterId, now, sitterClaims);
        announcer.Created(await store.SaveClaimedAsync(request, now, cancellationToken));
        LogClaimed(request.Id, command.SitterId);

        return RequestRead.From(request).ToView(now);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "RequestClaimed {RequestId} {MemberId}")]
    private partial void LogClaimed(Guid requestId, Guid memberId);
}
