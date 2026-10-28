using System.Threading.Channels;
using BabysittingPool.Application.Notifications;

namespace BabysittingPool.Infrastructure;

/// <summary>
/// In-process fan-out of Notification events to open connections, keyed by Member id (ADR 0019). One instance per
/// process; running several instances would need a shared bus. Each connection has a bounded queue: one that falls
/// behind is closed rather than blocking others or growing without limit, and its client reconnects and refetches.
/// </summary>
public sealed class NotificationBroadcaster : INotificationPublisher, INotificationSubscriptions
{
    private const int QueueCapacity = 64;

    private readonly Lock _gate = new();

    private readonly Dictionary<Guid, List<Subscription>> _byMember = [];

    public INotificationSubscription Subscribe(Guid memberId)
    {
        var subscription = new Subscription(this, memberId);
        lock (_gate)
        {
            if (!_byMember.TryGetValue(memberId, out var list))
            {
                _byMember[memberId] = list = [];
            }

            list.Add(subscription);
        }

        return subscription;
    }

    public void Publish(Guid memberId, NotificationEvent notificationEvent)
    {
        Subscription[] targets;
        lock (_gate)
        {
            targets = _byMember.TryGetValue(memberId, out var list) ? [.. list] : [];
        }

        foreach (var target in targets)
        {
            if (!target.Writer.TryWrite(notificationEvent))
            {
                target.Writer.TryComplete();
            }
        }
    }

    /// <summary>How many connections are open, for tests.</summary>
    public int ConnectionCount
    {
        get
        {
            lock (_gate)
            {
                return _byMember.Values.Sum(list => list.Count);
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (_byMember.TryGetValue(subscription.MemberId, out var list) && list.Remove(subscription) && list.Count == 0)
            {
                _byMember.Remove(subscription.MemberId);
            }
        }
    }

    private sealed class Subscription : INotificationSubscription
    {
        private readonly NotificationBroadcaster _owner;

        private readonly Channel<NotificationEvent> _channel =
            Channel.CreateBounded<NotificationEvent>(new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            });

        public Subscription(NotificationBroadcaster owner, Guid memberId)
        {
            _owner = owner;
            MemberId = memberId;
        }

        public Guid MemberId { get; }

        public ChannelReader<NotificationEvent> Events => _channel.Reader;

        public ChannelWriter<NotificationEvent> Writer => _channel.Writer;

        public void Dispose()
        {
            _owner.Remove(this);
            _channel.Writer.TryComplete();
        }
    }
}
