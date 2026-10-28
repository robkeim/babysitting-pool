using BabysittingPool.Application;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class RequestReader(BabysittingPoolDbContext db) : IRequestReader
{
    public async Task<Page<RequestView>> ListByRequesterAsync(
        Guid requesterId, DateTime now, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = await db.Requests.AsNoTracking().Where(r => r.RequesterId == requesterId)
            .ToKeysetPageAsync(page, r => r.PostedAt, r => r.Id, descending: true, cancellationToken);

        var sitterIds = rows.Where(r => r.SitterId is not null).Select(r => r.SitterId!.Value).Distinct().ToList();
        var sitterNames = await db.Members.AsNoTracking().Where(m => sitterIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.DisplayName, cancellationToken);

        return page.ToPage(rows, r => new KeysetPosition(r.PostedAt, r.Id), r =>
            ToRead(r).ToView(now, r.SitterId is { } sitterId ? sitterNames.GetValueOrDefault(sitterId) : null));
    }

    public async Task<Page<OpenRequestView>> ListOpenAsync(
        Guid excludeRequesterId, DateTime now, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = await db.Requests.AsNoTracking().Where(r =>
            r.Status == RequestStatus.Posted && r.ClaimCutoffAt > now && r.RequesterId != excludeRequesterId)
            .ToKeysetPageAsync(page, r => r.StartsAt, r => r.Id, descending: false, cancellationToken);

        return page.ToPage(rows, r => new KeysetPosition(r.StartsAt, r.Id), r => ToRead(r).ToOpenView());
    }

    public async Task<Page<RequestView>> ListClaimedByAsync(
        Guid sitterId, DateTime now, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = await db.Requests.AsNoTracking().Where(r => r.SitterId == sitterId)
            .ToKeysetPageAsync(page, r => r.StartsAt, r => r.Id, descending: true, cancellationToken);

        return page.ToPage(rows, r => new KeysetPosition(r.StartsAt, r.Id), r => ToRead(r).ToView(now));
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
