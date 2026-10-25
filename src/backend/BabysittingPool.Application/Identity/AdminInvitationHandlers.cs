using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

public sealed record InvitationView(
    Guid Id, string Email, bool IsAdmin, InvitationStatus Status, DateTime IssuedAt, DateTime ExpiresAt, DateTime? AcceptedAt)
{
    internal static InvitationView From(Invitation invitation, DateTime now) => new(
        invitation.Id,
        invitation.Email.Value,
        invitation.IsAdmin,
        invitation.StatusAt(now),
        invitation.IssuedAt,
        invitation.ExpiresAt,
        invitation.ConsumedAt);
}

/// <summary>Every Invitation, newest first, with whether it is pending, accepted or expired (credit-engine.md §2A).</summary>
public sealed class ListInvitationsHandler(IIdentityStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<InvitationView>> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return (await store.ListInvitationsAsync(cancellationToken)).Select(i => InvitationView.From(i, now)).ToList();
    }
}

public sealed record InviteMemberCommand(EmailAddress Email);

public enum InviteMemberOutcome
{
    Invited,
    AlreadyAMember,
    AlreadyInvited,
}

public sealed record InviteMemberResult(InviteMemberOutcome Outcome, InvitationView? Invitation);

/// <summary>
/// The Admin invites a person. An address with an unaccepted Invitation is refused: re-send that one instead,
/// so the list never holds two Invitations for one person.
/// </summary>
public sealed class InviteMemberHandler(IIdentityStore store, IssueInvitationHandler issue, TimeProvider clock)
{
    public async Task<InviteMemberResult> HandleAsync(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FindActiveMemberAsync(command.Email, cancellationToken) is not null)
        {
            return new InviteMemberResult(InviteMemberOutcome.AlreadyAMember, null);
        }

        if (await store.FindUnacceptedInvitationAsync(command.Email, cancellationToken) is not null)
        {
            return new InviteMemberResult(InviteMemberOutcome.AlreadyInvited, null);
        }

        await issue.HandleAsync(new IssueInvitationCommand(command.Email, IsAdmin: false), cancellationToken);
        var invitation = (await store.FindUnacceptedInvitationAsync(command.Email, cancellationToken))!;
        return new InviteMemberResult(InviteMemberOutcome.Invited, InvitationView.From(invitation, clock.GetUtcNow().UtcDateTime));
    }
}

public sealed record ResendInvitationCommand(Guid InvitationId);

public enum ResendInvitationOutcome
{
    Sent,
    NotFound,
    AlreadyAccepted,
    AlreadyAMember,
}

public sealed record ResendInvitationResult(ResendInvitationOutcome Outcome, InvitationView? Invitation);

/// <summary>
/// Gives a pending or expired Invitation a new link and a fresh validity and emails it. The old link stops
/// working, because only the hash of a secret is kept.
/// </summary>
public sealed class ResendInvitationHandler(
    IIdentityStore store, InvitationMailer mailer, CoopSettings settings, TimeProvider clock)
{
    public async Task<ResendInvitationResult> HandleAsync(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FindInvitationByIdAsync(command.InvitationId, cancellationToken) is not { } invitation)
        {
            return new ResendInvitationResult(ResendInvitationOutcome.NotFound, null);
        }

        if (invitation.ConsumedAt is not null)
        {
            return new ResendInvitationResult(ResendInvitationOutcome.AlreadyAccepted, null);
        }

        if (await store.FindActiveMemberAsync(invitation.Email, cancellationToken) is not null)
        {
            return new ResendInvitationResult(ResendInvitationOutcome.AlreadyAMember, null);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var secret = Secrets.Generate();
        var reissued = invitation.Reissue(Secrets.Hash(secret), now, settings.InvitationLifetime);
        if (!await store.TryReissueInvitationAsync(reissued, cancellationToken))
        {
            return new ResendInvitationResult(ResendInvitationOutcome.AlreadyAccepted, null);
        }

        await mailer.SendAsync(reissued.Email, secret, cancellationToken);
        return new ResendInvitationResult(ResendInvitationOutcome.Sent, InvitationView.From(reissued, now));
    }
}
