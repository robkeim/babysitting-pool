using System.Collections.Concurrent;
using BabysittingPool.Application.Identity;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Infrastructure;

/// <summary>
/// Development and E2E email: keeps each message in memory (ADR 0011, ADR 0012). With <paramref name="logMessages"/>
/// (Development only) it also logs the address and full link, which the never-log list otherwise forbids (ADR 0010).
/// Nothing is sent unless a real <paramref name="inner"/> sender is decorated (Development with SMTP secrets).
/// Never registered in Production.
/// </summary>
public sealed partial class EmailOutbox(ILogger<EmailOutbox> logger, IEmailSender? inner = null, bool logMessages = false) : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => [.. _messages];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Keep(message);
        return inner?.SendAsync(message, cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>Keeps and logs the message without delivering it, even when a real sender is decorated.</summary>
    public Task SendWithoutDeliveryAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Keep(message);
        return Task.CompletedTask;
    }

    private void Keep(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);
        if (logMessages)
        {
            LogMessage(logger, message.To, message.Subject, message.Link);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox email to {To}: {Subject} {Link}")]
    private static partial void LogMessage(ILogger logger, string to, string subject, Uri link);
}
