using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Application.Notifications;

/// <summary>A Notification that was just stored, and the Member it is for.</summary>
public sealed record NotificationDelivery(Guid MemberId, NotificationView Notification);

/// <summary>What the live stream carries (ADR 0019). The names are the SSE event names.</summary>
public abstract record NotificationEvent(string Name)
{
    public const string CreatedName = "notification.created";

    public const string DismissedName = "notification.dismissed";

    public const string ClearedName = "notifications.cleared";
}

/// <summary>A new Notification, in the same shape as a list item.</summary>
public sealed record NotificationCreated(NotificationView Notification) : NotificationEvent(CreatedName);

public sealed record NotificationDismissed(Guid Id) : NotificationEvent(DismissedName);

public sealed record NotificationsCleared() : NotificationEvent(ClearedName);

/// <summary>
/// Pushes events to the Member's open connections, if any (ADR 0019). Called after the transaction that stored the
/// rows has committed, so a failure here loses nothing: the rows are there to be listed.
/// </summary>
public interface INotificationPublisher
{
    void Publish(Guid memberId, NotificationEvent notificationEvent);
}

/// <summary>One open connection's feed. Disposing it ends the subscription.</summary>
public interface INotificationSubscription : IDisposable
{
    /// <summary>Completes when the feed is closed, including when the connection fell too far behind.</summary>
    ChannelReader<NotificationEvent> Events { get; }
}

public interface INotificationSubscriptions
{
    /// <summary>A Member may have several connections, each with its own subscription.</summary>
    INotificationSubscription Subscribe(Guid memberId);
}

/// <summary>Publishes after a commit and never throws: the data is already saved.</summary>
public sealed partial class NotificationAnnouncer(INotificationPublisher publisher, ILogger<NotificationAnnouncer> logger)
{
    public void Created(IEnumerable<NotificationDelivery> deliveries)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        foreach (var delivery in deliveries)
        {
            Announce(delivery.MemberId, new NotificationCreated(delivery.Notification));
        }
    }

    public void Dismissed(Guid memberId, Guid notificationId) =>
        Announce(memberId, new NotificationDismissed(notificationId));

    public void Cleared(Guid memberId) => Announce(memberId, new NotificationsCleared());

    private void Announce(Guid memberId, NotificationEvent notificationEvent)
    {
        try
        {
            publisher.Publish(memberId, notificationEvent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPublishFailed(ex, notificationEvent.Name, memberId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "NotificationPublishFailed {Event} {MemberId}")]
    private partial void LogPublishFailed(Exception exception, string @event, Guid memberId);
}
