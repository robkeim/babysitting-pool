namespace BabysittingPool.Application.Notifications;

public sealed class ListNotificationsHandler(INotificationReader reader)
{
    /// <summary>Throws <see cref="InvalidCursorException"/> for a cursor this API did not issue.</summary>
    public Task<Page<NotificationView>> HandleAsync(
        Guid memberId, string? cursor, int? limit, CancellationToken cancellationToken) =>
        reader.ListAsync(memberId, PageRequest.From(cursor, limit), cancellationToken);
}

/// <summary>Dismissing hard-deletes the row; there is no read state (ADR 0019).</summary>
public sealed class DismissNotificationHandler(INotificationStore store, NotificationAnnouncer announcer)
{
    /// <summary>False when the Notification belongs to another Member.</summary>
    public async Task<bool> HandleAsync(Guid memberId, Guid notificationId, CancellationToken cancellationToken)
    {
        if (!await store.DismissAsync(memberId, notificationId, cancellationToken))
        {
            return false;
        }

        // The caller's other open tabs drop it too.
        announcer.Dismissed(memberId, notificationId);
        return true;
    }
}

public sealed class DismissAllNotificationsHandler(INotificationStore store, NotificationAnnouncer announcer)
{
    public async Task HandleAsync(Guid memberId, CancellationToken cancellationToken)
    {
        await store.DismissAllAsync(memberId, cancellationToken);
        announcer.Cleared(memberId);
    }
}
