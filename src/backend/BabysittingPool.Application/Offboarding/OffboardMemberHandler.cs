using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using Microsoft.Extensions.DependencyInjection;
using CreditAmount = BabysittingPool.Domain.Credits.Credits;

namespace BabysittingPool.Application.Offboarding;

public sealed record OffboardMemberCommand(Guid MemberId);

/// <summary>
/// What Offboarding did: Requests cancelled, claims withdrawn, Requests already due that were settled instead, and
/// the balance burned.
/// </summary>
public sealed record OffboardMemberResult(int Cancelled, int Withdrawn, int Settled, long BurnedQuarterCredits);

/// <summary>
/// The Admin closes a Member's account (spec §2A). The account closes first, so the Member can do nothing more, then
/// each open Request they posted or claimed is released in its own transaction (ADR 0004), and finally the remaining
/// Available Balance is burned. Safe to repeat: after a failure the Admin runs it again and it picks up what is left.
/// </summary>
public sealed class OffboardMemberHandler(
    IIdentityStore identity, IRequestReader reader, IServiceScopeFactory scopes, TimeProvider clock)
{
    /// <summary>
    /// The outcome, or null when there is no such Member. Offboarding the Admin throws <see cref="DomainException"/>;
    /// a concurrent change throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<OffboardMemberResult?> HandleAsync(OffboardMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await identity.FindMemberAsync(command.MemberId, cancellationToken) is not { } member)
        {
            return null;
        }

        if (!member.IsClosed)
        {
            member.Close(now);
            await identity.CloseMemberAsync(member.Id, now, cancellationToken);
        }

        int cancelled = 0, withdrawn = 0, settled = 0;
        foreach (var id in await reader.ListOpenInvolvingAsync(member.Id, cancellationToken))
        {
            // A scope per Request is its own transaction and its own tracked state (ADR 0004).
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IRequestStore>();
            switch (await ReleaseAsync(store, id, member.Id, now, cancellationToken))
            {
                case Release.Cancelled: cancelled++; break;
                case Release.Withdrawn: withdrawn++; break;
                case Release.Settled: settled++; break;
            }
        }

        var burned = await BurnAsync(member.Id, now, cancellationToken);
        return new OffboardMemberResult(cancelled, withdrawn, settled, burned.QuarterCredits);
    }

    private enum Release { Skipped, Cancelled, Withdrawn, Settled }

    private async Task<CreditAmount> BurnAsync(Guid memberId, DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRequestStore>();
        var account = await store.FindAccountAsync(memberId, cancellationToken)
            ?? throw new InvalidOperationException("The Member has no Credit Account.");

        var burned = account.BurnAll();
        if (burned > 0m)
        {
            await store.SaveBurnedAsync(memberId, account, burned, now, cancellationToken);
        }

        return CreditAmount.FromDecimal(burned);
    }

    /// <summary>
    /// Posted: cancelled, or expired once past the Claim Cutoff. Claimed before its start: cancelled by the Requester
    /// or withdrawn by the Sitter. Claimed and already started: completed, as Settle would, so the Sitter is paid
    /// and nothing stays in Escrow.
    /// </summary>
    private static async Task<Release> ReleaseAsync(
        IRequestStore store, Guid requestId, Guid memberId, DateTime now, CancellationToken cancellationToken)
    {
        if (await store.FindRequestAsync(requestId, cancellationToken) is not { Status: RequestStatus.Posted or RequestStatus.Claimed } request)
        {
            return Release.Skipped;
        }

        var requester = await store.FindAccountAsync(request.RequesterId, cancellationToken)
            ?? throw new InvalidOperationException("The Requester has no Credit Account.");

        if (request.Status == RequestStatus.Claimed && now >= request.StartsAt)
        {
            var sitter = await store.FindAccountAsync(request.SitterId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("The Sitter has no Credit Account.");
            request.Complete(requester, sitter, now);
            await store.SaveCompletedAsync(request, requester, sitter, now, cancellationToken);
            return Release.Settled;
        }

        if (request.Status == RequestStatus.Posted && now >= request.ClaimCutoffAt)
        {
            request.Expire(requester, now);
            await store.SaveExpiredAsync(request, requester, now, cancellationToken);
            return Release.Settled;
        }

        if (request.RequesterId == memberId)
        {
            request.Cancel(memberId, requester, now);
            await store.SaveReleasedAsync(request, requester, now, cancellationToken);
            return Release.Cancelled;
        }

        request.Withdraw(memberId, requester, now);
        await store.SaveReleasedAsync(request, requester, now, cancellationToken);
        return Release.Withdrawn;
    }
}
