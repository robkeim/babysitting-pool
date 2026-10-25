using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

public sealed record IssueInvitationCommand(EmailAddress Email, bool IsAdmin);

public enum IssueInvitationResult
{
    Issued,
    AlreadyAMember,
    AdminAlreadyExists,
}

/// <summary>Creates an Invitation and emails the link (credit-engine.md §2A).</summary>
public sealed class IssueInvitationHandler(
    IIdentityStore store, IEmailSender email, AppLinks links, CoopSettings settings, TimeProvider clock)
{
    public async Task<IssueInvitationResult> HandleAsync(IssueInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FindActiveMemberAsync(command.Email, cancellationToken) is not null)
        {
            return IssueInvitationResult.AlreadyAMember;
        }

        if (command.IsAdmin && await store.AdminExistsAsync(cancellationToken))
        {
            return IssueInvitationResult.AdminAlreadyExists;
        }

        var secret = Secrets.Generate();
        var invitation = Invitation.Issue(
            command.Email, command.IsAdmin, Secrets.Hash(secret), clock.GetUtcNow().UtcDateTime, settings.InvitationLifetime);
        await store.AddInvitationAsync(invitation, cancellationToken);

        var link = links.Invitation(secret);
        await email.SendAsync(
            new EmailMessage(
                command.Email.Value,
                "You're invited to the babysitting co-op",
                $"You've been invited to join the babysitting co-op. Use this link to create your account (it works once and expires in {settings.InvitationLifetime.TotalDays:0} days):\n\n{link}",
                link),
            cancellationToken);
        return IssueInvitationResult.Issued;
    }
}
