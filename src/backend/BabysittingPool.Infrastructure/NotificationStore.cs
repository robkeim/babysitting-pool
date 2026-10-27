using BabysittingPool.Application;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class NotificationStore(BabysittingPoolDbContext db) : INotificationStore, INotificationReader
{
    public async Task<bool> DismissAsync(Guid memberId, Guid notificationId, CancellationToken cancellationToken)
    {
        var deleted = await db.Notifications.Where(n => n.Id == notificationId && n.MemberId == memberId)
            .ExecuteDeleteAsync(cancellationToken);
        // Nothing deleted: already dismissed (fine, idempotent) or someone else's (not found).
        return deleted > 0 || !await db.Notifications.AnyAsync(n => n.Id == notificationId, cancellationToken);
    }

    public async Task DismissAllAsync(Guid memberId, CancellationToken cancellationToken) =>
        await db.Notifications.Where(n => n.MemberId == memberId).ExecuteDeleteAsync(cancellationToken);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        await db.Notifications.Where(n => n.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);

    public async Task<Page<NotificationView>> ListAsync(Guid memberId, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = await db.Notifications.AsNoTracking().Where(n => n.MemberId == memberId)
            .ToKeysetPageAsync(page, n => n.CreatedAt, n => n.Id, descending: true, cancellationToken);

        return page.ToPage(
            rows,
            n => new KeysetPosition(n.CreatedAt, n.Id),
            n => n.ToView());
    }
}
