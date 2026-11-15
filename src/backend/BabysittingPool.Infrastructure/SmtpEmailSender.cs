using BabysittingPool.Application.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace BabysittingPool.Infrastructure;

/// <summary>Where and as whom to send. <see cref="Address"/> is the base address, used as the From.</summary>
public sealed record SmtpSettings(string Address, string Password, string Host = "smtp.gmail.com", int Port = 587);

/// <summary>Sends plain-text email over SMTP with STARTTLS (ADR 0011). Failures propagate to the caller.</summary>
public sealed class SmtpEmailSender(SmtpSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.Address));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(TextFormat.Plain) { Text = message.Body };

        // The chain and host name are still validated; only the online revocation lookup is skipped, because it
        // often cannot complete (macOS) and then fails the TLS handshake.
        using var client = new SmtpClient { CheckCertificateRevocation = false };
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(settings.Address, settings.Password, cancellationToken);
        await client.SendAsync(new SmtpSendRequest(mime), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
