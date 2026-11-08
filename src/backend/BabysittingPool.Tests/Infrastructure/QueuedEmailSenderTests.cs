using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace BabysittingPool.Tests.Infrastructure;

/// <summary>A slow or failing sender must not change how long a sign-in link request takes (ADR 0012).</summary>
public class QueuedEmailSenderTests
{
    private static EmailMessage Message(string to) => new(to, "Subject", "Body", new Uri("https://coop.example/x"));

    [Fact]
    public async Task Send_returns_while_the_inner_sender_is_still_blocked()
    {
        var inner = new GatedSender();
        var queue = new QueuedEmailSender(inner, NullLogger<QueuedEmailSender>.Instance);
        using var stop = new CancellationTokenSource();
        var delivery = queue.DeliverAsync(stop.Token);

        await queue.SendAsync(Message("ada@example.com"), CancellationToken.None);

        inner.Sent.Should().BeEmpty();
        inner.Open();
        await inner.WaitForAsync(1);
        inner.Sent.Should().ContainSingle().Which.To.Should().Be("ada@example.com");
        await stop.CancelAsync();
        await delivery;
    }

    [Fact]
    public async Task A_failed_delivery_does_not_stop_the_next_one()
    {
        var inner = new GatedSender { FailFor = "bad@example.com" };
        inner.Open();
        var queue = new QueuedEmailSender(inner, NullLogger<QueuedEmailSender>.Instance);
        using var stop = new CancellationTokenSource();
        var delivery = queue.DeliverAsync(stop.Token);

        await queue.SendAsync(Message("bad@example.com"), CancellationToken.None);
        await queue.SendAsync(Message("good@example.com"), CancellationToken.None);

        await inner.WaitForAsync(1);
        inner.Sent.Should().ContainSingle().Which.To.Should().Be("good@example.com");
        await stop.CancelAsync();
        await delivery;
    }

    private sealed class GatedSender : IEmailSender
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<EmailMessage> _sent = [];

        public string? FailFor { get; init; }

        public IReadOnlyList<EmailMessage> Sent { get { lock (_sent) { return [.. _sent]; } } }

        public void Open() => _gate.SetResult();

        public async Task WaitForAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Sent.Count < count)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken);
            if (message.To == FailFor)
            {
                throw new InvalidOperationException("SMTP down");
            }

            lock (_sent)
            {
                _sent.Add(message);
            }
        }
    }
}
