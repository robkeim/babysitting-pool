using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record EditRequestNotesCommand(Guid RequestId, Guid RequesterId, string? Notes);

/// <summary>
/// The Requester sets or clears the notes of a Posted or Claimed Request until its start time (spec §4.6). Only the
/// notes change, so a claim stands, and nobody is notified (ADR 0014).
/// </summary>
public sealed class EditRequestNotesHandler(IRequestStore store, TimeProvider clock)
{
    /// <summary>
    /// The Request with its new notes, or null when it does not exist or is not the caller's (ADR 0014). Rule
    /// violations throw <see cref="DomainException"/>; a concurrent change throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<RequestView?> HandleAsync(EditRequestNotesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await store.FindRequestAsync(command.RequestId, cancellationToken) is not { } request
            || request.RequesterId != command.RequesterId)
        {
            return null;
        }

        request.EditNotes(command.RequesterId, command.Notes, now);
        await store.SaveNotesAsync(request, cancellationToken);

        return RequestRead.From(request).ToView(now);
    }
}
