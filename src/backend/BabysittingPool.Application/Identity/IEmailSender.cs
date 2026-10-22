namespace BabysittingPool.Application.Identity;

public sealed record EmailMessage(string To, string Subject, string Body, Uri Link);

/// <summary>Port for outgoing email. Only sign-in and Invitation emails exist in v1.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
