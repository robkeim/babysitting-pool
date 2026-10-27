namespace BabysittingPool.Application.Notifications;

public sealed class ListNotificationsHandler(INotificationReader reader)
{
    /// <summary>Throws <see cref="InvalidCursorException"/> for a cursor this API did not issue.</summary>
    public Task<Page<NotificationView>> HandleAsync(
        Guid memberId, string? cursor, int? limit, CancellationToken cancellationToken) =>
        reader.ListAsync(memberId, PageRequest.From(cursor, limit), cancellationToken);
}

/// <summary>Dismissing hard-deletes the row; there is no read state (ADR 0019).</summary>
public sealed class DismissNotificationHandler(INotificationStore store)
{
    /// <summary>False when the Notification belongs to another Member.</summary>
    public Task<bool> HandleAsync(Guid memberId, Guid notificationId, CancellationToken cancellationToken) =>
        store.DismissAsync(memberId, notificationId, cancellationToken);
}

public sealed class DismissAllNotificationsHandler(INotificationStore store)
{
    public Task HandleAsync(Guid memberId, CancellationToken cancellationToken) =>
        store.DismissAllAsync(memberId, cancellationToken);
}
