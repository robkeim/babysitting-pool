using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>Emails an Invitation link; shared by issuing and re-sending (credit-engine.md §2A).</summary>
public sealed class InvitationMailer(IEmailSender email, AppLinks links, CoopSettings settings)
{
    public async Task SendAsync(EmailAddress to, string secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);
        var link = links.Invitation(secret);
        await email.SendAsync(
            new EmailMessage(
                to.Value,
                "You're invited to the babysitting co-op",
                $"You've been invited to join the babysitting co-op. Use this link to create your account (it works once and expires in {settings.InvitationLifetime.TotalDays:0} days):\n\n{link}",
                link),
            cancellationToken);
    }
}
