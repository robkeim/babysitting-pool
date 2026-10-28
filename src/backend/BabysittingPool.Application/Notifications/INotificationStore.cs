namespace BabysittingPool.Application.Notifications;

/// <summary>Write port for dismissing Notifications. Creating them is part of saving a Request (ADR 0004).</summary>
public interface INotificationStore
{
    /// <summary>
    /// Deletes the Member's Notification. False only when the id belongs to another Member; an id that is already
    /// gone is a success, so dismissing is idempotent.
    /// </summary>
    Task<bool> DismissAsync(Guid memberId, Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Deletes every Notification of the Member.</summary>
    Task DismissAllAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>Deletes every Notification created before <paramref name="cutoff"/>, whoever it is for. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
}

/// <summary>Read port (ADR 0002).</summary>
public interface INotificationReader
{
    /// <summary>The Member's Notifications, newest first, one page as <paramref name="page"/> asks.</summary>
    Task<Page<NotificationView>> ListAsync(Guid memberId, PageRequest page, CancellationToken cancellationToken);
}
