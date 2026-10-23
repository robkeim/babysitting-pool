using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class RequestReader(BabysittingPoolDbContext db) : IRequestReader
{
    public async Task<IReadOnlyList<RequestRead>> ListByRequesterAsync(
        Guid requesterId, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken)
    {
        var requests = db.Requests.AsNoTracking().Where(r => r.RequesterId == requesterId);
        if (after is { } position)
        {
            requests = requests.Where(r => r.PostedAt < position.At
                || (r.PostedAt == position.At && r.Id.CompareTo(position.Id) < 0));
        }

        var rows = await requests
            .OrderByDescending(r => r.PostedAt).ThenByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRead).ToList();
    }

    public async Task<IReadOnlyList<RequestRead>> ListOpenAsync(
        Guid excludeRequesterId, DateTime now, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken)
    {
        var requests = db.Requests.AsNoTracking().Where(r =>
            r.Status == RequestStatus.Posted && r.ClaimCutoffAt > now && r.RequesterId != excludeRequesterId);
        if (after is { } position)
        {
            requests = requests.Where(r => r.StartsAt > position.At
                || (r.StartsAt == position.At && r.Id.CompareTo(position.Id) > 0));
        }

        var rows = await requests
            .OrderBy(r => r.StartsAt).ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRead).ToList();
    }

    public async Task<IReadOnlyList<RequestRead>> ListClaimedByAsync(
        Guid sitterId, (DateTime At, Guid Id)? after, int take, CancellationToken cancellationToken)
    {
        var requests = db.Requests.AsNoTracking().Where(r => r.SitterId == sitterId);
        if (after is { } position)
        {
            requests = requests.Where(r => r.StartsAt < position.At
                || (r.StartsAt == position.At && r.Id.CompareTo(position.Id) < 0));
        }

        var rows = await requests
            .OrderByDescending(r => r.StartsAt).ThenByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRead).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListDueForExpiryAsync(DateTime now, CancellationToken cancellationToken) =>
        await db.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Posted && r.ClaimCutoffAt <= now)
            .OrderBy(r => r.ClaimCutoffAt).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListDueForPayoutAsync(DateTime now, CancellationToken cancellationToken) =>
        await db.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Claimed && r.StartsAt <= now)
            .OrderBy(r => r.StartsAt).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListOpenInvolvingAsync(Guid memberId, CancellationToken cancellationToken) =>
        await db.Requests.AsNoTracking()
            .Where(r => (r.Status == RequestStatus.Posted || r.Status == RequestStatus.Claimed)
                && (r.RequesterId == memberId || r.SitterId == memberId))
            .OrderBy(r => r.StartsAt).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    private static RequestRead ToRead(RequestRecord r) => new(
        r.Id, r.StartsAt, r.DurationMinutes, r.ChildCount, r.Location, r.Transport, r.Meals, r.Area, r.Address,
        r.Cost.QuarterCredits, r.ClaimCutoffAt, r.PostedAt, r.Status);
}
