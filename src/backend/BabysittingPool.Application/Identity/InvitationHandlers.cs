using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>Shows who an Invitation is for, so the account form can say so. Null if the link is not usable.</summary>
public sealed class PreviewInvitationHandler(IIdentityStore store, TimeProvider clock)
{
    public async Task<string?> HandleAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await store.FindInvitationAsync(Secrets.Hash(token), cancellationToken);
        return invitation is not null && invitation.IsUsableAt(clock.GetUtcNow().UtcDateTime) ? invitation.Email.Value : null;
    }
}

public sealed record AcceptInvitationCommand(string Token, DisplayName DisplayName);

/// <summary>A new Member's session secret, to be set as the cookie.</summary>
public sealed record SignedIn(string SessionSecret);

/// <summary>Creates the Member, their Credit Account with the Starting Balance, and a Session from an Invitation.</summary>
public sealed class AcceptInvitationHandler(IIdentityStore store, TimeProvider clock)
{
    /// <summary>Null if the Invitation is unknown, used, expired, or can no longer be honored.</summary>
    public async Task<SignedIn?> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        var invitation = await store.FindInvitationAsync(Secrets.Hash(command.Token), cancellationToken);
        if (invitation is null || !invitation.IsUsableAt(now)
            || await store.FindActiveMemberAsync(invitation.Email, cancellationToken) is not null
            || (invitation.IsAdmin && await store.AdminExistsAsync(cancellationToken)))
        {
            return null;
        }

        var member = Member.Join(invitation.Email, command.DisplayName, invitation.IsAdmin, now);
        var sessionSecret = Secrets.Generate();
        var session = Session.Start(member.Id, Secrets.Hash(sessionSecret), now);
        return await store.TryAcceptInvitationAsync(invitation, member, session, now, cancellationToken)
            ? new SignedIn(sessionSecret)
            : null;
    }
}
