using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record ClaimRequestCommand(Guid RequestId, Guid SitterId);

/// <summary>The first Sitter to claim a Posted Request wins; there is no approval step (spec §4.4).</summary>
public sealed class ClaimRequestHandler(IRequestStore store, TimeProvider clock)
{
    /// <summary>
    /// The claimed Request as its Sitter sees it, or null when it does not exist. Rule violations throw
    /// <see cref="DomainException"/>; losing the race throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<RequestView?> HandleAsync(ClaimRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await store.FindRequestAsync(command.RequestId, cancellationToken) is not { } request)
        {
            return null;
        }

        request.Claim(command.SitterId, now);
        await store.SaveClaimedAsync(request, cancellationToken);

        return RequestRead.From(request).ToView(now);
    }
}
