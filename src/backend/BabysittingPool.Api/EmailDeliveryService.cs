using BabysittingPool.Infrastructure;

namespace BabysittingPool.Api;

/// <summary>Delivers the mail the request path queued over SMTP (ADR 0012). Idle where the fake outbox is in use.</summary>
internal sealed class EmailDeliveryService(IEnumerable<QueuedEmailSender> queues) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(queues.Select(queue => queue.DeliverAsync(stoppingToken)));
}
