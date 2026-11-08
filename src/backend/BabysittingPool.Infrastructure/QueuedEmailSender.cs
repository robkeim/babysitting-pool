using System.Threading.Channels;
using BabysittingPool.Application.Identity;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Infrastructure;

/// <summary>
/// Hands each message to a queue and returns at once, so a request does the same work whether or not mail goes out
/// and response time cannot reveal who is a Member (ADR 0012). <see cref="DeliverAsync"/> drains the queue into the
/// real <paramref name="inner"/> sender; a failed delivery is logged and dropped, never retried.
/// </summary>
public sealed partial class QueuedEmailSender(IEmailSender inner, ILogger<QueuedEmailSender> logger) : IEmailSender
{
    private readonly Channel<EmailMessage> _queue = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        _queue.Writer.TryWrite(message);
        return Task.CompletedTask;
    }

    /// <summary>Delivers queued messages one at a time until <paramref name="stoppingToken"/> is canceled.</summary>
    public async Task DeliverAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await inner.SendAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The address stays out of the log; the subject says which kind of mail failed.
                    LogDeliveryFailed(ex, message.Subject);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "EmailDeliveryFailed: {Subject}")]
    private partial void LogDeliveryFailed(Exception exception, string subject);
}
