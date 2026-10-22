using BabysittingPool.Application.Requests;
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

        return rows.Select(r => new RequestRead(
            r.Id, r.StartsAt, r.DurationMinutes, r.ChildCount, r.Location, r.Transport, r.Meals, r.Area, r.Address,
            r.Cost.QuarterCredits, r.ClaimCutoffAt, r.PostedAt, r.Status)).ToList();
    }
}
